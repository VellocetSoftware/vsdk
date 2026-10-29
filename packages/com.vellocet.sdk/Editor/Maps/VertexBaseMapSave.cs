// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vellocet.Sdk.Maps;
using Vertex.Contracts;
using Vertex.Unity;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>
    /// Saves a Vertex map as a game base map. The map folder keeps only the Vertex sources, the owner file, the
    /// definition, the thumbnail and one <c>Compiled</c> folder; the imported mesh and source scene are built in the
    /// ignored <c>Assets/VertexBuild</c> workspace and deleted once the compiled scene no longer needs them.
    /// </summary>
    public static class VertexBaseMapSave
    {
        private const string Workspace = UnityBuildProtocol.AssetRoot;
        private const string Input = Workspace + "/Input/";
        private const string OwnerFile = "vertex-owner.json";
        [Serializable] private sealed class Owner { public int version = 1; public string mapId; }

        public static Func<VertexEditorMapContext, string> Create(BaseMapSettings settings) => context => Save(settings, context);

        private static string Save(BaseMapSettings settings, VertexEditorMapContext context)
        {
            var job = context.Job;
            var parsed = MapAsset.Deserialize(File.ReadAllBytes(context.PathFor(job.Map)));
            VertexMapCompiler.ValidateAuthoring(job, context.PathFor, parsed);
            var id = job.RuntimeMapId;
            BuildProtocol.ValidateMapContentId(id);
            var folder = ResolveFolder(settings.MapsRoot, id, job.MapId);
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                context.Report(.02, "Importing the Vertex map sources…");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (AssetDatabase.IsValidFolder(Workspace)) AssetDatabase.DeleteAsset(Workspace);
                Directory.CreateDirectory(folder);
                WriteIfChanged(folder + "/" + OwnerFile, JsonUtility.ToJson(new Owner { mapId = job.MapId.ToString("D") }, true) + "\n");
                // Sources stay beside the definition; everything else is a build input of this save only.
                WriteIfChanged(folder + "/" + id + ".vertex", File.ReadAllBytes(context.PathFor("VertexJob/Map.vertex")));
                foreach (var file in context.Inputs.Files.Where(file => file.Path.StartsWith(Input, StringComparison.Ordinal)))
                {
                    context.CheckCanceled();
                    var bytes = File.ReadAllBytes(context.PathFor(file.Path));
                    if (file.Path.StartsWith(Input + "Scripts/", StringComparison.Ordinal))
                        WriteIfChanged(folder + "/" + file.Path.Substring((Input + "Scripts/").Length), bytes, keepExistingMeta: true);
                    else WriteIfChanged(file.Path, bytes);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var script = folder + "/" + job.Script.Substring((Input + "Scripts/").Length);
                var sourcePath = Workspace + "/Source/" + id + ".unity";
                // Register the generated folder before the scene builder validates its asset path.
                if (!MapEditorPathUtility.EnsureAssetFolder(Path.GetDirectoryName(sourcePath)))
                    throw new IOException("Could not create the generated scene folder: " + sourcePath);
                File.WriteAllText(sourcePath + ".meta", "fileFormatVersion: 2\nguid: " +
                    AssetCatalog.Hash(Encoding.UTF8.GetBytes(job.ProjectId + "/" + job.MapId + "/" + sourcePath)).Substring(0, 32) + "\n");
                var resources = VertexSdkRegistry.CaptureResources(job, context.Root, context.Inputs);
                VertexMapSceneBuilder.CreateSourceScene(job.Map, script, sourcePath, resources, context.Compile, parsed);
                context.CheckCanceled();
                // A save without compilation is a quick look at the imported map; it never touches the shipped map.
                if (!context.Compile) return sourcePath;

                var map = EnsureDefinition(settings, folder, id, job.MapTitle ?? id);
                var previousSource = map.authoringScenePath;
                if (job.Thumbnail != null) AdoptThumbnail(map, job.Thumbnail, folder);
                var lightmaps = VertexBakePolicy.Lightmaps(parsed, job.Game.Baking);
                map = BaseMapCompile.Compile(settings, map, sourcePath, new MapCompiler.CompileOptions
                {
                    BakePolicy = job.Game.Baking,
                    LightmapQuality = VertexBakePolicy.Quality(lightmaps),
                    SkipLightingBake = lightmaps == "None",
                    RequireNonDevelopmentBuild = true,
                    FinalizeLegacyGeometry = false,
                    ScriptSource = AssetDatabase.LoadAssetAtPath<TextAsset>(script),
                    BeforeSaveScene = scene => PrepareExternalContent(job, scene),
                    ReportProgress = (fraction, message) => context.Report(.05 + .9 * fraction, message),
                    Log = Debug.Log
                }, keepsSource: false);
                context.Report(.96, "Removing build inputs the compiled map does not reference…");
                AdoptReferencedInputs(map.compiledScenePath);
                AssetDatabase.DeleteAsset(Workspace);
                RemovePreviousLayout(folder, previousSource);
                return map.compiledScenePath;
            }
            finally
            {
                if (scenes.Length > 0 && scenes.All(scene => !string.IsNullOrEmpty(scene.path) && File.Exists(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }
        }

        private static string ResolveFolder(string root, string id, Guid mapId)
        {
            var owned = Directory.Exists(root)
                ? Directory.GetFiles(root, OwnerFile, SearchOption.AllDirectories).Where(path => ReadOwner(path) == mapId).ToArray()
                : Array.Empty<string>();
            if (owned.Length > 1) throw new InvalidDataException("Multiple map folders claim this Vertex map: " + string.Join(", ", owned));
            if (owned.Length == 1) return Path.GetDirectoryName(owned[0])!.Replace('\\', '/');
            var folder = root.TrimEnd('/') + "/" + id;
            if (File.Exists(folder + "/" + OwnerFile) || File.Exists(folder + "/MapDefinition.asset"))
                throw new InvalidDataException("Another map owns " + folder + ". Rename this map before saving.");
            return folder;
        }

        private static Guid ReadOwner(string path)
        {
            try
            {
                return JsonUtility.FromJson<Owner>(File.ReadAllText(path)) is { version: 1 } owner &&
                       Guid.TryParse(owner.mapId, out var id) ? id : Guid.Empty;
            }
            catch (ArgumentException) { return Guid.Empty; }
        }

        private static MapDefinition EnsureDefinition(BaseMapSettings settings, string folder, string id, string title)
        {
            var path = folder + "/MapDefinition.asset";
            if (AssetDatabase.FindAssets("t:MapDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Any(other => other != path && AssetDatabase.LoadAssetAtPath<MapDefinition>(other)?.id == id))
                throw new InvalidDataException("Another base map already uses ID " + id);
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(path);
            if (!map)
            {
                map = ScriptableObject.CreateInstance<MapDefinition>();
                map.id = id;
                map.author = settings.Author;
                map.version = settings.Version;
                map.defaultModeId = settings.DefaultModeId;
                map.contentKind = MapCompilationReceipt.BaseContentKind;
                AssetDatabase.CreateAsset(map, path);
            }
            if (map.id != id) throw new InvalidDataException(path + " belongs to map " + map.id);
            map.displayName = title;
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
            return map;
        }

        private static void AdoptThumbnail(MapDefinition map, string input, string folder)
        {
            var target = folder + "/Thumbnail" + Path.GetExtension(input);
            WriteIfChanged(target, File.ReadAllBytes(input));
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            VertexMapCompiler.LoadThumbnail(target);
            map.thumbnailReference = new AssetReference(AssetDatabase.AssetPathToGUID(target));
            EditorUtility.SetDirty(map);
        }

        private static void PrepareExternalContent(BuildJob job, UnityEngine.SceneManagement.Scene scene)
        {
            var external = (job.Content ?? Array.Empty<BuildContent>()).Where(content => content.External).ToArray();
            if (external.Length > 0)
                new MapPackageAssets(Array.Empty<VertexAssetCatalogProfile>(), null, external, externalContentOnly: true).Prepare(scene);
        }

        /// <summary>Moves any workspace asset the compiled scene still references into the compiled folder (GUIDs kept).</summary>
        private static void AdoptReferencedInputs(string compiledScenePath)
        {
            var target = Path.GetDirectoryName(compiledScenePath)!.Replace('\\', '/') + "/Inputs/";
            foreach (var path in AssetDatabase.GetDependencies(compiledScenePath, true)
                         .Where(path => path.StartsWith(Input, StringComparison.Ordinal)))
            {
                var destination = target + path.Substring(Input.Length);
                MapEditorPathUtility.EnsureAssetFolder(Path.GetDirectoryName(destination)!.Replace('\\', '/'));
                var error = AssetDatabase.MoveAsset(path, destination);
                if (!string.IsNullOrEmpty(error)) throw new IOException("Could not keep compiled map input " + path + ": " + error);
                Debug.LogWarning("Compiled map still references a Vertex build input; kept it at " + destination);
            }
        }

        /// <summary>Deletes outputs of the per-game save flows this one replaces.</summary>
        private static void RemovePreviousLayout(string folder, string previousSource)
        {
            var stale = new[] { folder + "/Generated", folder + "/Source", folder + "/Authoring.unity", folder + "/Authoring" }
                .Concat(string.IsNullOrEmpty(previousSource) || !previousSource.StartsWith(folder + "/", StringComparison.Ordinal)
                    ? Array.Empty<string>()
                    : new[] { previousSource, Path.ChangeExtension(previousSource, null) });
            foreach (var path in stale.Distinct())
                if (File.Exists(path) || AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path);
        }

        private static void WriteIfChanged(string path, string text) => WriteIfChanged(path, Encoding.UTF8.GetBytes(text));

        private static void WriteIfChanged(string path, byte[] bytes, bool keepExistingMeta = false)
        {
            // Existing metas already carry the GUIDs scenes reference; never replace them with captured ones.
            if (keepExistingMeta && path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && File.Exists(path)) return;
            if (File.Exists(path) && new FileInfo(path).Length == bytes.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }
    }
}
