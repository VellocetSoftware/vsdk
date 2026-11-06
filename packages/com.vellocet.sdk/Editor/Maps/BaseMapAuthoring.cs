using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vellocet.Foundation.Editor;
using Vellocet.Sdk.Editor.Tools;
using Vellocet.Sdk.Editor.VSig;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class BaseMapAuthoring
    {
        public static IEnumerable<MapDefinition> Definitions() => AssetDatabase.FindAssets("t:MapDefinition", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<MapDefinition>).Where(map => map && !map.IsAddon);

        public static bool IsVertexOwned(MapDefinition map)
        {
            if (!map || !string.IsNullOrEmpty(map.authoringScenePath)) return false;
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(map));
            return !string.IsNullOrEmpty(folder) && File.Exists(Path.Combine(folder, "vertex-owner.json"));
        }

        public static bool TryValidateSource(MapDefinition map, out string error, string sourceScenePath = null)
        {
            if (!map || map.IsAddon) { error = "Select a base map definition."; return false; }
            if (!TryValidateIdentityAndFolder(map, out error)) return false;
            var path = sourceScenePath ?? map.authoringScenePath;
            if (string.IsNullOrEmpty(path))
            {
                error = IsVertexOwned(map) ? "Save this map from Vertex to compile it." : "Assign an Authoring Scene.";
                return false;
            }
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
            { error = "Authoring Scene must be a saved Unity scene under Assets."; return false; }
            foreach (var other in Definitions())
                if (path.StartsWith(BaseMapCompile.CompiledFolder(other) + "/", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, other.compiledScenePath, StringComparison.OrdinalIgnoreCase))
                { error = "Choose an authoring scene, not compiled output."; return false; }
            error = null;
            return true;
        }

        public static bool TryValidateIdentityAndFolder(MapDefinition map, out string error)
        {
            error = null;
            if (!map || map.IsAddon) { error = "Select a base map definition."; return false; }
            if (!ModeCatalog.TryResolveMapMode(map.id, out _))
            { error = "Map ID must be lowercase and begin with a registered mode prefix followed by '_' and a name."; return false; }
            var path = AssetDatabase.GetAssetPath(map);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            { error = "Save the map definition under Assets."; return false; }
            var folder = Path.GetDirectoryName(path);
            foreach (var other in Definitions())
            {
                if (other == map) continue;
                var otherPath = AssetDatabase.GetAssetPath(other);
                if (path.StartsWith(BaseMapCompile.CompiledFolder(other) + "/", StringComparison.OrdinalIgnoreCase) ||
                    otherPath.StartsWith(BaseMapCompile.CompiledFolder(map) + "/", StringComparison.OrdinalIgnoreCase))
                { error = "Keep map definitions outside Compiled output folders: " + otherPath; return false; }
                if (string.Equals(map.id, other.id, StringComparison.OrdinalIgnoreCase))
                { error = "Another map already uses ID '" + map.id + "': " + AssetDatabase.GetAssetPath(other); return false; }
                if (string.Equals(folder, Path.GetDirectoryName(AssetDatabase.GetAssetPath(other)), StringComparison.OrdinalIgnoreCase))
                { error = "Each map definition needs its own folder; these definitions would share Compiled output."; return false; }
            }
            return true;
        }

        public static MapDefinition Compile(MapDefinition map)
        {
            EditorCommandState.RequireMapCompilation();
            if (!TryValidateSource(map, out var error)) throw new InvalidOperationException(error);
            var settings = SdkHost.Actions?.BaseMaps ?? throw new InvalidOperationException("Base-map compilation is not registered.");
            var status = VSigMapSourceUtility.FindForScene(map.authoringScenePath, out var script, out _, out var expected);
            if (status == VSigMapSourceStatus.Multiple)
                throw new InvalidOperationException("Multiple VSig sources were found. Name the intended script '" + expected + "'.");
            var options = settings.CreateCompileOptions?.Invoke() ?? new MapCompiler.CompileOptions
            {
                BakePolicy = VertexBakePolicy.Create(), LightmapQuality = MapLightmapQuality.High,
                RequireNonDevelopmentBuild = true
            };
            options.ScriptSource = script;
            options.ReportProgress = (fraction, message) =>
            {
                if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Compile " + map.id, message, fraction))
                    throw new OperationCanceledException("Compilation canceled; previous output restored.");
            };
            try { return BaseMapCompile.Compile(settings, map, map.authoringScenePath, options, keepsSource: true); }
            finally { if (!Application.isBatchMode) EditorUtility.ClearProgressBar(); }
        }

        public static bool TryValidateCompiled(MapDefinition map, out string error)
        {
            if (!TryValidateIdentityAndFolder(map, out error)) return false;
            if (map.IsDraft) { error = "Compile the draft map first."; return false; }
            if (!File.Exists(map.compiledScenePath) || map.sceneReference == null ||
                AssetDatabase.GUIDToAssetPath(map.sceneReference.AssetGUID) != map.compiledScenePath)
            { error = "Compiled scene is missing or its reference is stale. Compile the map."; return false; }
            if (!MapCompilationReceiptUtility.TryLoad(map.compilationReceiptPath, out var receipt) ||
                !MapCompilationReceipt.IsSupportedSchemaVersion(receipt.schemaVersion) ||
                !MapCompilationReceiptUtility.HasValidSeal(receipt) ||
                MapCompilationReceiptValidator.GetIdentityMismatch(receipt, MapCompilationReceipt.BaseContentKind,
                    map.id, null, StringComparison.Ordinal) != MapCompilationReceiptValidator.IdentityMismatch.None ||
                receipt.seal != map.compilationSeal || receipt.compiledScenePath != map.compiledScenePath ||
                MapCompilationReceiptUtility.ComputeContentRevision(receipt) != map.contentRevision)
            { error = "Compilation receipt is missing or stale. Compile the map."; return false; }
            if (!string.IsNullOrEmpty(map.authoringScenePath) && !AssetDatabase.LoadAssetAtPath<SceneAsset>(map.authoringScenePath))
            { error = "The authoring scene is missing."; return false; }
            if (!HasCurrentHash(map.compiledScenePath, receipt.compiledSceneHash) ||
                !string.IsNullOrEmpty(map.authoringScenePath) &&
                (map.authoringScenePath != receipt.sourceScenePath || !HasCurrentHash(map.authoringScenePath, receipt.sourceSceneHash)))
            { error = "Scene content changed since compilation. Compile the map."; return false; }
            error = null;
            return true;
        }

        private static bool HasCurrentHash(string path, string expected) =>
            Utilities.SdkFileHashUtility.TryComputeSha256(path, out var actual) &&
            string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

        public static IReadOnlyList<string> ValidateAll()
        {
            var errors = new List<string>();
            foreach (var map in Definitions().Where(map => map.Enabled))
                if (!TryValidateCompiled(map, out var error)) errors.Add(map.id + ": " + error);
            return errors;
        }
    }
}
