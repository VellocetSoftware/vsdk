using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vellocet.Sdk;
using Vellocet.Sdk.Editor.Maps;
using Vertex.Contracts;
using Vertex.Unity.Editor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Vellocet.Sdk.Editor.Distribution
{
    /// <summary>One self-contained SDK compiler and authoring catalog. Never distributes the game project.</summary>
    public abstract class SdkPublisher
    {
        protected abstract string GamePackage { get; }
        protected abstract VertexAssetCatalogProfile EnsureAssetProfile();
        protected abstract GameProfile ExportGameProfile(VertexAssetCatalogProfile profile, string destination);
        protected virtual SdkTool[] ResourceTools => Array.Empty<SdkTool>();
        protected virtual void PrepareCompiler(string compiler, GameProfile game) { }

        public void Build(string outputRoot, string toolsRoot)
        {
            var output = SdkArtifactPathUtility.ToAbsolutePath(outputRoot);
            SdkArtifactPathUtility.EnsurePathWithinRepository(output);
            if (!outputRoot.Replace('\\', '/').StartsWith("Builds/", StringComparison.Ordinal))
                throw new InvalidDataException("SDK distributions must be written under Builds/.");
            var toolchain = JObject.Parse(File.ReadAllText(Path.Combine(toolsRoot, "toolchain.json")));
            ValidateTools(toolsRoot);
            toolchain[SdkGame.Current.Id] = SourceRevision();
            var name = SdkGame.Current.DisplayName.Replace(' ', '_');
            var profile = EnsureAssetProfile();
            var stage = output + ".staging-" + Guid.NewGuid().ToString("N");
            var previous = output + ".previous";
            try
            {
                Directory.CreateDirectory(stage);
                var content = stage + "/Content";
                var compiler = stage + "/Compiler";
                var package = compiler + "/Packages/com.vellocet.sdk";
                var exported = SdkContentExporter.ExportToOutputRoot(SdkArtifactPathUtility.ToRepositoryRelativePath(content));
                if (!exported.Succeeded) throw new InvalidDataException(SdkContentExporter.BuildSummary(exported));
                var validation = SdkContentExporter.ValidateOutputRoot(SdkArtifactPathUtility.ToRepositoryRelativePath(content));
                if (!validation.Succeeded) throw new InvalidDataException(SdkContentExporter.BuildValidationSummary(validation));
                var sdk = SdkPackageExporter.ExportToOutputRoot(SdkArtifactPathUtility.ToRepositoryRelativePath(package));
                if (!sdk.Succeeded) throw new InvalidDataException(SdkPackageExporter.BuildSummary(sdk));
                Directory.Move(content + "/Assets", compiler + "/Assets");
                Directory.CreateDirectory(compiler + "/ProjectSettings");
                File.Copy(content + "/" + Constants.SdkContentManifestFileName,
                    compiler + "/ProjectSettings/VellocetSdkContentOwnership.json");
                Directory.Delete(content, true);
                EmbedPackages(compiler);
                if (SdkGame.Current.AudioRuntimeCapability == Vellocet.Sdk.Audio.AddonAudioBanks.Capability)
                {
                    var audioInputs = Path.GetFullPath(Path.Combine(SdkArtifactPathUtility.GetRepositoryRoot(), "Incoming/Stereoform"));
                    var audioBuild = JObject.Parse(File.ReadAllText(Path.Combine(audioInputs, "runtime-build.json")));
                    if ((int)audioBuild["abi"] != 2) throw new InvalidDataException("Incompatible Stereoform SDK artifact.");
                    toolchain["stereoform"] = audioBuild["revision"];
                    CopyTree(Path.Combine(audioInputs, "Compiler"), compiler + "/StereoformTools");
                    File.Copy("ProjectSettings/AudioManager.asset", compiler + "/ProjectSettings/AudioManager.asset");
                }
                foreach (var setting in new[] { "ProjectVersion.txt", "TagManager.asset", "DynamicsManager.asset", "NavMeshAreas.asset" })
                    File.Copy("ProjectSettings/" + setting, compiler + "/ProjectSettings/" + setting);

                Directory.CreateDirectory(compiler + "/Assets/CustomContent");
                File.WriteAllText(compiler + "/Assets/CustomContent/.keep", "");
                var gamePath = stage + "/Authoring/" + name + ".vertexgame";
                Directory.CreateDirectory(Path.GetDirectoryName(gamePath));
                var game = ExportGameProfile(profile, gamePath);
                game.Build.EditorSaveLabel = null;
                PrepareCompiler(compiler, game);
                WriteAssetProfile(profile, compiler);
                WriteRenderingSettings(compiler);
                File.WriteAllBytes(gamePath, game.Serialize());
                File.WriteAllBytes(compiler + "/" + VertexSdkProject.GamePath, game.Serialize());
                WriteInstalledAddresses(compiler);
                File.Copy(package + "/LICENSE.txt", stage + "/LICENSE.txt");
                CopyTree(Path.Combine(toolsRoot, "Editor"), stage + "/Editor");
                CopyTree(Path.Combine(toolsRoot, "Launcher"), stage + "/Launcher");
                File.WriteAllText(stage + "/toolchain.json", toolchain.ToString());

                var inventory = new BuildInputs { Files = Directory.GetFiles(compiler, "*", SearchOption.AllDirectories)
                    .Select(path => new BuildFile { Path = Path.GetRelativePath(compiler, path).Replace('\\', '/'),
                        Bytes = new FileInfo(path).Length, Sha256 = Hash(path) }).OrderBy(file => file.Path, StringComparer.Ordinal).ToArray() };
                inventory.Validate();
                File.WriteAllBytes(stage + "/Compiler.inputs.json", BuildProtocol.Serialize(inventory));
                File.WriteAllBytes(stage + "/" + name + ".vertexsdk", new SdkInstallation
                {
                    Game = new ProfileReference { Id = game.Id, Version = game.Version, Path = "Authoring/" + name + ".vertexgame" },
                    CompilerProject = "Compiler", CompilerInputs = "Compiler.inputs.json",
                    ResourceTools = ResourceTools
                }.Serialize());
                if (Directory.Exists(previous)) Directory.Delete(previous, true);
                if (Directory.Exists(output)) Directory.Move(output, previous);
                try { Directory.Move(stage, output); }
                catch { if (Directory.Exists(previous)) Directory.Move(previous, output); throw; }
                Debug.Log("VERTEX_SDK_PUBLISHED: " + output + " (" + inventory.Files.Length + " verified compiler files)");
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }

        internal static void ValidateTools(string toolsRoot)
        {
            foreach (var runtime in new[] { "win-x64", "osx-arm64", "osx-x64" })
            foreach (var tool in new[]
                     {
                         "Editor/" + runtime + (runtime == "win-x64" ? "/Vertex.exe" : "/Vertex.app/Contents/MacOS/Vertex"),
                         "Launcher/" + runtime + "/VSDK" + (runtime == "win-x64" ? ".exe" : "")
                     })
            {
                var path = Path.Combine(toolsRoot, tool);
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                    throw new FileNotFoundException("Incomplete VSDK toolkit: " + path + ". Pass the complete toolkit with -sdkToolsRoot.", path);
            }
        }

        private static string SourceRevision()
        {
            var command = new System.Diagnostics.ProcessStartInfo("git")
                { UseShellExecute = false, RedirectStandardOutput = true, WorkingDirectory = SdkArtifactPathUtility.GetRepositoryRoot() };
            command.ArgumentList.Add("rev-parse"); command.ArgumentList.Add("HEAD");
            using var process = System.Diagnostics.Process.Start(command) ?? throw new IOException("Cannot read the SDK source revision.");
            var revision = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0 || revision.Length != 40) throw new IOException("Cannot read the SDK source revision.");
            return revision;
        }

        private void EmbedPackages(string compiler)
        {
            var registered = PackageInfo.GetAllRegisteredPackages().ToDictionary(package => package.name, StringComparer.Ordinal);
            var pending = new Queue<string>(new[] { "com.vellocet.sdk", GamePackage, "com.vellocet.vertex", "com.unity.render-pipelines.universal" });
            // Engine modules are part of Unity, not copied game code or remotely resolved packages.
            foreach (var package in registered.Values.Where(package => package.source == PackageSource.BuiltIn)) pending.Enqueue(package.name);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var dependencies = new JObject();
            while (pending.Count > 0)
            {
                var name = pending.Dequeue();
                if (!seen.Add(name) || name.StartsWith("com.unity.test-framework", StringComparison.Ordinal)) continue;
                if (!registered.TryGetValue(name, out var package)) throw new InvalidDataException("SDK dependency is not installed: " + name);
                foreach (var dependency in package.dependencies) pending.Enqueue(dependency.name);
                if (package.source == PackageSource.BuiltIn) dependencies[name] = package.version;
                else
                {
                    var destination = compiler + "/Packages/" + name;
                    if (name != Constants.PackageId) CopyTree(package.resolvedPath, destination);
                    var manifestPath = destination + "/package.json";
                    var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                    if (manifest["dependencies"] is JObject requirements)
                        foreach (var dependency in requirements.Properties().Where(property =>
                                     property.Name.StartsWith("com.unity.test-framework", StringComparison.Ordinal)).ToArray())
                            dependency.Remove();
                    File.WriteAllText(manifestPath, manifest.ToString());
                    dependencies[name] = "file:" + name;
                }
            }
            File.WriteAllText(compiler + "/Packages/manifest.json", new JObject { ["dependencies"] = dependencies }.ToString());
        }

        private void WriteAssetProfile(VertexAssetCatalogProfile original, string compiler)
        {
            // The compiler needs asset authorization, not references to editor-only preview assets.
            var copy = ScriptableObject.CreateInstance<VertexAssetCatalogProfile>();
            copy.catalogId = original.catalogId;
            copy.catalogVersion = original.catalogVersion;
            var temporary = "Assets/VertexSdkExport-" + Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                copy.folders = Constants.SdkAssetRootMappings.Where(mapping => Directory.Exists(compiler + "/" + mapping.InstalledRoot))
                    .Select(mapping => new VertexAssetFolder { folder = mapping.InstalledRoot, ownership = "base", textures = true, audio = true, text = true, data = true })
                    .Concat(new[] { "Assets/VertexSdk/BaseScripts", "Packages/" + GamePackage + "/Runtime/VSig/Libraries" }
                        .Where(folder => Directory.Exists(compiler + "/" + folder)).Select(folder => new VertexAssetFolder
                            { folder = folder, ownership = "base", materials = false, models = false, prefabs = false, text = true }))
                    .Append(new VertexAssetFolder { folder = "Assets/CustomContent", ownership = "custom", textures = true, audio = true, text = true, data = true }).ToArray();
                AssetDatabase.CreateAsset(copy, temporary);
                AssetDatabase.SaveAssetIfDirty(copy);
                Directory.CreateDirectory(compiler + "/Assets/VertexSdk");
                File.Copy(temporary, compiler + "/Assets/VertexSdk/Assets.asset");
                File.Copy(temporary + ".meta", compiler + "/Assets/VertexSdk/Assets.asset.meta");
            }
            finally { AssetDatabase.DeleteAsset(temporary); }
        }

        private static void WriteRenderingSettings(string compiler)
        {
            var pipelines = Enumerable.Range(0, QualitySettings.names.Length)
                .Select(QualitySettings.GetRenderPipelineAssetAt).Prepend(GraphicsSettings.defaultRenderPipeline)
                .Where(asset => asset).Distinct().Cast<UniversalRenderPipelineAsset>().ToArray();
            if (pipelines.Length == 0 || !GraphicsSettings.defaultRenderPipeline)
                throw new InvalidDataException("The game must supply its default URP pipeline before publishing the SDK.");
            var temporary = "Assets/VertexSdkRendering-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(temporary));
            var renderers = new Dictionary<ScriptableRendererData, ScriptableRendererData>();
            try
            {
                for (var i = 0; i < pipelines.Length; i++)
                {
                    var pipeline = UnityEngine.Object.Instantiate(pipelines[i]);
                    AssetDatabase.CreateAsset(pipeline, temporary + (i == 0 ? "/Pipeline.asset" : "/Quality-" + i + ".asset"));
                    var serialized = new SerializedObject(pipeline);
                    var list = serialized.FindProperty("m_RendererDataList");
                    for (var j = 0; j < list.arraySize; j++)
                    {
                        var slot = list.GetArrayElementAtIndex(j);
                        if (slot.objectReferenceValue is not ScriptableRendererData source) continue;
                        if (!renderers.TryGetValue(source, out var renderer))
                        {
                            renderer = UnityEngine.Object.Instantiate(source);
                            renderer.rendererFeatures.Clear();
                            AssetDatabase.CreateAsset(renderer, temporary + "/Renderer-" + renderers.Count + ".asset");
                            // Only native URP features affect its variant stripping; game effects stay in the game.
                            foreach (var feature in source.rendererFeatures.Where(feature => feature &&
                                         feature.GetType().Assembly == typeof(UniversalRendererData).Assembly))
                            {
                                var copy = UnityEngine.Object.Instantiate(feature);
                                AssetDatabase.AddObjectToAsset(copy, renderer);
                                renderer.rendererFeatures.Add(copy);
                            }
                            var rendererSettings = new SerializedObject(renderer);
                            rendererSettings.FindProperty("m_RendererFeatureMap").ClearArray();
                            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(renderer);
                            renderers.Add(source, renderer);
                        }
                        slot.objectReferenceValue = renderer;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pipeline);
                }
                AssetDatabase.SaveAssets();
                // Never pull game scripts or effects into the public compiler through a renderer dependency.
                foreach (var dependency in AssetDatabase.GetDependencies(Directory.GetFiles(temporary, "*.asset"), true))
                    if (!dependency.StartsWith(temporary + "/", StringComparison.Ordinal) &&
                        (!dependency.StartsWith("Packages/", StringComparison.Ordinal) ||
                         !(File.Exists(compiler + "/" + dependency) || PackageInfo.FindForAssetPath(dependency)?.source == PackageSource.BuiltIn)))
                        throw new InvalidDataException("Unpublished SDK rendering dependency: " + dependency);
                var destination = compiler + "/Assets/VertexSdk/Rendering";
                CopyTree(temporary, destination);
                // Preserve pipeline GUIDs so Unity's quality settings retain every supported graphics tier.
                for (var i = 0; i < pipelines.Length; i++)
                    File.Copy(AssetDatabase.GetAssetPath(pipelines[i]) + ".meta",
                        destination + (i == 0 ? "/Pipeline.asset.meta" : "/Quality-" + i + ".asset.meta"), true);
                File.Copy("ProjectSettings/QualitySettings.asset", compiler + "/ProjectSettings/QualitySettings.asset");
            }
            finally { AssetDatabase.DeleteAsset(temporary); }
        }

        private static void WriteInstalledAddresses(string compiler)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (!settings) throw new InvalidDataException("The game needs its installed base Addressables before publishing the SDK.");
            var assets = new List<VertexSdkProject.InstalledAddress>();
            foreach (var meta in Directory.GetFiles(compiler + "/Assets", "*.meta", SearchOption.AllDirectories)
                         .Concat(new[] { compiler + "/" + VertexDeveloperTextureImporter.AssetPath + ".meta" }))
            {
                if (!File.Exists(meta.Substring(0, meta.Length - 5))) continue;
                var line = File.ReadLines(meta).FirstOrDefault(value => value.StartsWith("guid: ", StringComparison.Ordinal));
                if (line == null) continue;
                var guid = line.Substring(6).Trim();
                var entry = settings.FindAssetEntry(guid);
                if (entry != null) assets.Add(new VertexSdkProject.InstalledAddress { guid = guid, address = entry.address });
            }
            File.WriteAllText(compiler + "/ProjectSettings/VertexBaseAddresses.json", JsonUtility.ToJson(new VertexSdkProject.InstalledAddresses { assets = assets.ToArray() }, true));
        }

        private static string Hash(string file)
        {
            using var stream = File.OpenRead(file); using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        protected static void CopyTree(string source, string destination)
        {
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("SDK distribution cannot contain directory links: " + source);
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                if (SdkPackageExporter.ShouldSkipPackagePath(Path.GetFileName(file))) continue;
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("SDK distribution cannot contain file links: " + file);
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }
            foreach (var directory in Directory.GetDirectories(source))
            {
                var name = Path.GetFileName(directory);
                if (SdkPackageExporter.ShouldSkipPackagePath(name)) continue;
                CopyTree(directory, Path.Combine(destination, name));
            }
        }
    }
}
