// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Maps;
using Vertex.Contracts;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>Game policy for base maps that ship with the game.</summary>
    public sealed class BaseMapSettings
    {
        /// <summary>Folder that holds one subfolder per base map, e.g. <c>Assets/GameContent/Maps</c>.</summary>
        public string MapsRoot;
        /// <summary>Defaults for a new MapDefinition; existing definitions keep their values.</summary>
        public string Author = "", Version = "1";
        /// <summary>Runs before every compile, e.g. to validate project settings or regenerate content catalogs.</summary>
        public Action Prepare;
        public Func<MapCompiler.CompileOptions> CreateCompileOptions;
        /// <summary>Registers the published map with the game's content catalog (Addressables etc.).</summary>
        public Action<MapDefinition> Publish;
        /// <summary>Returns an error for a compiled map the game cannot ship, given the source and compiled scene paths.</summary>
        public Func<string, string, string> Verify;
    }

    /// <summary>
    /// Compiles a base map and restores its output, definition and Addressables if compilation or publication fails.
    /// </summary>
    public static class BaseMapCompile
    {
        public const string CompiledFolderName = "Compiled";
        private const string BackupRoot = "Library/VellocetBaseMaps";

        public static string CompiledFolder(MapDefinition map) =>
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(map))?.Replace('\\', '/') + "/" + CompiledFolderName;

        public static MapDefinition Compile(BaseMapSettings settings, MapDefinition map, string sourceScenePath,
            MapCompiler.CompileOptions options, bool keepsSource, string scenePath = null, string receiptPath = null)
        {
            Vellocet.Foundation.Editor.EditorCommandState.RequireMapCompilation();
            if (!BaseMapAuthoring.TryValidateSource(map, out var sourceError, sourceScenePath))
                throw new InvalidOperationException(sourceError);
            if (!MapEntityIntegrityValidator.TryCaptureInIsolation(sourceScenePath, out var authored, out var integrityError))
                throw new InvalidDataException(integrityError);
            options ??= new MapCompiler.CompileOptions();
            if (Lightmapping.isRunning || StaticOcclusionCulling.isRunning || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for the Editor and any active bake to finish.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save the open scenes before compiling a map.");
            var definitionPath = AssetDatabase.GetAssetPath(map);
            var layout = BaseMapArtifactLayout.FromDefinitionPath(definitionPath, map.id);
            var folder = layout.CompiledRoot;
            if (sourceScenePath.StartsWith(folder + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Choose the authoring scene, not its compiled output.");
            scenePath ??= layout.CompiledScenePath;
            receiptPath ??= layout.ReceiptPath;
            var definitionRoot = Path.GetDirectoryName(definitionPath).Replace('\\', '/') + "/";
            var outputs = scenePath.StartsWith(folder + "/", StringComparison.Ordinal) &&
                          receiptPath.StartsWith(folder + "/", StringComparison.Ordinal)
                ? new[] { folder }
                : new[] { scenePath, Path.ChangeExtension(scenePath, null),
                    MapSpatialGraphArtifacts.ResolveAssetPath(scenePath, map.id), receiptPath };
            foreach (var output in outputs)
            {
                if (!output.StartsWith(definitionRoot, StringComparison.Ordinal) || output.Contains("/../") ||
                    output == definitionPath || output == sourceScenePath ||
                    definitionPath.StartsWith(output + "/", StringComparison.OrdinalIgnoreCase) ||
                    sourceScenePath.StartsWith(output + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Compiled output must stay in the map folder and cannot overwrite authoring content: " + output);
                if (BaseMapAuthoring.Definitions().Any(other => other != map &&
                        AssetDatabase.GetAssetPath(other).StartsWith(output + "/", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Compiled output would overwrite another map definition: " + output);
            }
            // A stable scene GUID keeps the map's Addressables entry and content revision across recompiles.
            var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(sceneGuid)) sceneGuid = AssetCatalog.Hash(Encoding.UTF8.GetBytes("base-map/" + map.id)).Substring(0, 32);

            var backup = new Backup(BackupRoot + "/" + map.id, outputs, definitionPath, settings.Prepare);
            var development = EditorUserBuildSettings.development;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
                File.WriteAllText(scenePath + ".meta", "fileFormatVersion: 2\nguid: " + sceneGuid + "\n");
                options.SyncBakedArtifactsToAuthoring = false; // Bakes belong to the compiled scene only.
                if (options.RequireNonDevelopmentBuild) EditorUserBuildSettings.development = false;
                var result = MapCompiler.Compile(AssetDatabase.LoadAssetAtPath<SceneAsset>(sourceScenePath), map.id, map.id,
                    MapCompilationReceipt.BaseContentKind, scenePath, options);
                if (!result.Success) throw new InvalidDataException(result.Error ?? "Compilation failed for " + map.id);
                if (!MapEntityIntegrityValidator.TryCaptureInIsolation(scenePath, out var compiled, out integrityError) ||
                    !MapEntityIntegrityValidator.TryCompare(authored, compiled, out integrityError))
                    throw new InvalidDataException(integrityError);
                if (settings.Verify?.Invoke(sourceScenePath, scenePath) is { } verifyError) throw new InvalidDataException(verifyError);

                // Scene reloads and bake imports invalidate previously loaded asset objects.
                map = AssetDatabase.LoadAssetAtPath<MapDefinition>(definitionPath) ??
                      throw new InvalidOperationException("The map definition was removed during compilation.");
                var receipt = result.Receipt;
                receipt.mapTitle = map.displayName;
                MapCompilationReceiptUtility.Seal(receipt);
                Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
                File.WriteAllText(receiptPath, MapCompilationReceiptUtility.Serialize(receipt));
                AssetDatabase.ImportAsset(receiptPath);

                map.contentKind = MapCompilationReceipt.BaseContentKind;
                if (string.IsNullOrEmpty(map.sceneAddress)) map.sceneAddress = receipt.sceneAddress;
                map.authoringScenePath = keepsSource ? sourceScenePath : string.Empty;
                map.compiledScenePath = scenePath;
                map.compilationReceiptPath = receiptPath;
                map.compilationSeal = receipt.seal;
                map.contentRevision = MapCompilationReceiptUtility.ComputeContentRevision(receipt);
                map.compiledUtc = receipt.createdUtc;
                map.musicSet = result.MusicSet;
                map.MarkCompiled();
                map.sceneReference = new AssetReference(AssetDatabase.AssetPathToGUID(scenePath));
                EditorUtility.SetDirty(map);
                AssetDatabase.SaveAssetIfDirty(map);
                SharedAssetAddressableEditorUtility.EnsureSdkSharedAssets(AddressableAssetSettingsDefaultObject.GetSettings(true));
                settings.Publish?.Invoke(map);
                AssetDatabase.SaveAssets();
                backup.Discard();
                return AssetDatabase.LoadAssetAtPath<MapDefinition>(definitionPath);
            }
            catch
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                backup.Restore();
                throw;
            }
            finally
            {
                EditorUserBuildSettings.development = development;
            }
        }

        private sealed class Backup
        {
            private const string Addresses = "Assets/AddressableAssetsData";
            private readonly string root;

            [Serializable]
            private sealed class Manifest
            {
                public string definition;
                public string[] outputs;
                public bool[] existed;
                public bool addressesExisted;
            }

            public Backup(string root, string[] outputs, string definition, Action prepare)
            {
                this.root = root;
                if (Directory.Exists(root)) Restore();
                // Preparation validates published maps, so it must see their previous output.
                prepare?.Invoke();
                Directory.CreateDirectory(root);
                var manifest = new Manifest
                {
                    definition = definition,
                    outputs = outputs.SelectMany(path => new[] { path, path + ".meta" }).ToArray(),
                    addressesExisted = Directory.Exists(Addresses)
                };
                manifest.existed = manifest.outputs.Select(Exists).ToArray();
                Copy(definition, root + "/MapDefinition.asset");
                Copy(definition + ".meta", root + "/MapDefinition.asset.meta");
                if (manifest.addressesExisted) FileUtil.CopyFileOrDirectory(Addresses, root + "/Addresses");
                File.WriteAllText(root + "/pending.json", JsonUtility.ToJson(manifest));
                try
                {
                    for (var i = 0; i < manifest.outputs.Length; i++)
                        Move(manifest.outputs[i], root + "/Output" + i);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
                catch { Restore(); throw; }
            }

            public void Restore()
            {
                var manifestPath = root + "/pending.json";
                if (!File.Exists(manifestPath))
                    throw new InvalidDataException("Map backup is incomplete; recover it before recompiling: " + root);
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
                for (var i = 0; i < manifest.outputs.Length; i++)
                {
                    var saved = root + "/Output" + i;
                    if (Exists(saved) || !manifest.existed[i]) Delete(manifest.outputs[i]);
                    Move(saved, manifest.outputs[i]);
                }
                Copy(root + "/MapDefinition.asset", manifest.definition);
                Copy(root + "/MapDefinition.asset.meta", manifest.definition + ".meta");
                if (Directory.Exists(root + "/Addresses") || !manifest.addressesExisted) Delete(Addresses);
                Move(root + "/Addresses", Addresses);
                Discard();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            public void Discard()
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (IOException error) { Debug.LogWarning("Map backup cleanup deferred: " + error.Message); }
            }

            private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
            private static void Delete(string path)
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
            }
            private static void Move(string from, string to)
            {
                if (!Exists(from)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                if (Directory.Exists(from)) Directory.Move(from, to);
                else File.Move(from, to);
            }
            private static void Copy(string from, string to)
            {
                if (File.Exists(from)) File.Copy(from, to, true);
            }
        }
    }
}
