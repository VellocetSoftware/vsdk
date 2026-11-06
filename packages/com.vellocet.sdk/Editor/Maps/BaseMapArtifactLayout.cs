#if UNITY_EDITOR
using System.IO;
using Vellocet.Sdk;
using Vellocet.Sdk.Editor.Maps;

namespace Vellocet.Sdk.Editor.Maps
{
    public readonly struct BaseMapArtifactLayout
    {
        public BaseMapArtifactLayout(string mapId, string definitionPath, string compiledRoot,
            string compiledScenePath, string receiptPath, string spatialGraphAssetPath, string spatialGraphAddress)
        {
            MapId = ContentIdUtility.Sanitize(mapId) ?? string.Empty;
            SceneAddress = MapId;
            DefinitionPath = NormalizeAssetPath(definitionPath);
            CompiledRoot = NormalizeAssetPath(compiledRoot);
            CompiledScenePath = NormalizeAssetPath(compiledScenePath);
            ReceiptPath = NormalizeAssetPath(receiptPath);
            SpatialGraphAssetPath = NormalizeAssetPath(spatialGraphAssetPath);
            SpatialGraphAddress = spatialGraphAddress ?? string.Empty;
        }

        public string MapId { get; }
        public string SceneAddress { get; }
        public string DefinitionPath { get; }
        public string CompiledRoot { get; }
        public string CompiledScenePath { get; }
        public string ReceiptPath { get; }
        public string SpatialGraphAssetPath { get; }
        public string SpatialGraphAddress { get; }

        public static BaseMapArtifactLayout FromDefinitionPath(string definitionPath, string mapId)
        {
            definitionPath = NormalizeAssetPath(definitionPath);
            var normalizedMapId = ContentIdUtility.Sanitize(mapId);
            var compiledRoot = ResolveCompiledRoot(definitionPath);
            var compiledScenePath = string.IsNullOrWhiteSpace(compiledRoot) ||
                                    string.IsNullOrWhiteSpace(normalizedMapId)
                ? null
                : $"{compiledRoot}/{normalizedMapId}.unity";
            return new BaseMapArtifactLayout(
                normalizedMapId,
                definitionPath,
                compiledRoot,
                compiledScenePath,
                string.IsNullOrWhiteSpace(compiledRoot)
                    ? null
                    : $"{compiledRoot}/{Constants.CompilationReceiptFileName}",
                MapSpatialGraphArtifacts.ResolveAssetPathFromCompiledRoot(compiledRoot, normalizedMapId),
                MapSpatialGraphArtifacts.ResolveAddress(normalizedMapId));
        }

        private static string ResolveCompiledRoot(string definitionPath)
        {
            if (string.IsNullOrWhiteSpace(definitionPath))
                return null;

            var folder = Path.GetDirectoryName(definitionPath)?.Replace('\\', '/');
            return string.IsNullOrWhiteSpace(folder) ? null : $"{folder}/{BaseMapCompile.CompiledFolderName}";
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/');
        }
    }
}
#endif