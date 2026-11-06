using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vellocet.Foundation.Editor;
using Vellocet.Sdk.Editor.Distribution;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Maps;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Tools
{
    public sealed class SdkEditorActions
    {
        public BaseMapSettings BaseMaps;
        public VertexDeveloperConnection DeveloperConnection;
        public Func<string, GameProfile> ExportGameProfile;
        public Action<string, string> BuildDistribution;
        public string DistributionRoot;
        public string ToolsRoot;
    }

    public static class SdkEditorCommands
    {
        public const string OpenVertexPath = EditorMenuPaths.Root + "SDK/Open Vertex Map Editor…";
        public const string RefreshPath = EditorMenuPaths.Root + "SDK/Refresh Vertex Developer Connection";
        public const string ExportProfilePath = EditorMenuPaths.Root + "SDK/Export Game Profile…";
        public const string DistributionPath = EditorMenuPaths.Root + "SDK/Build Distribution…";
        public const string CompilePath = EditorMenuPaths.Root + "Content/Maps/Compile Map";
        public const string CompileContextPath = "CONTEXT/MapDefinition/Compile Map";

        [MenuItem(OpenVertexPath, priority = 501)]
        public static void OpenVertex()
        {
            EditorCommandState.RequireEdit();
            VertexEditorLauncher.Open(SdkHost.Actions?.DeveloperConnection ??
                throw new InvalidOperationException("This project has no Vertex developer connection."));
        }

        [MenuItem(RefreshPath, priority = 502)]
        public static void RefreshConnection()
        {
            EditorCommandState.RequireEdit();
            (SdkHost.Actions?.DeveloperConnection ?? throw new InvalidOperationException("This project has no Vertex developer connection.")).Publish();
        }

        [MenuItem(ExportProfilePath, priority = 503)]
        public static void ExportProfile()
        {
            EditorCommandState.RequireEdit();
            var export = SdkHost.Actions?.ExportGameProfile ?? throw new InvalidOperationException("Game profile export is not registered.");
            var path = EditorUtility.SaveFilePanel("Export game profile", "", SdkGame.Current.DisplayName + ".vertexgame", "vertexgame");
            if (!string.IsNullOrEmpty(path)) export(path);
        }

        [MenuItem(DistributionPath, priority = 700)]
        public static void BuildDistribution()
        {
            EditorCommandState.RequireEdit();
            var actions = SdkHost.Actions;
            if (actions?.BuildDistribution == null) throw new InvalidOperationException("SDK distribution is not registered.");
            var output = EditorUtility.SaveFolderPanel("SDK distribution output", SdkArtifactPathUtility.ToAbsolutePath(actions.DistributionRoot), "");
            if (string.IsNullOrEmpty(output)) return;
            var tools = EditorUtility.OpenFolderPanel("VSDK tools (contains toolchain.json)", actions.ToolsRoot, "");
            if (!string.IsNullOrEmpty(tools)) actions.BuildDistribution(output, tools);
        }

        public static void CreateMapDefinition()
        {
            EditorCommandState.RequireEdit();
            ProjectWindowUtil.CreateAsset(ScriptableObject.CreateInstance<MapDefinition>(), "MapDefinition.asset");
        }

        [MenuItem(CompilePath, priority = 250)]
        private static void CompileSelected() => BaseMapAuthoring.Compile(Selection.activeObject as MapDefinition);
        [MenuItem(CompileContextPath)]
        private static void CompileContext(MenuCommand command) => BaseMapAuthoring.Compile(command.context as MapDefinition);

        [MenuItem(OpenVertexPath, true)]
        [MenuItem(RefreshPath, true)]
        private static bool CanOpenVertex() => EditorCommandState.CanEdit && SdkHost.Actions?.DeveloperConnection != null;
        [MenuItem(ExportProfilePath, true)] private static bool CanExport() => EditorCommandState.CanEdit && SdkHost.Actions?.ExportGameProfile != null;
        [MenuItem(DistributionPath, true)] private static bool CanBuildDistribution() => EditorCommandState.CanEdit && SdkHost.Actions?.BuildDistribution != null;
        [MenuItem(CompilePath, true)] private static bool CanCompileSelected() => CanCompile(Selection.activeObject as MapDefinition);
        [MenuItem(CompileContextPath, true)] private static bool CanCompileContext(MenuCommand command) => CanCompile(command.context as MapDefinition);
        public static bool CanCompile(MapDefinition map) => EditorCommandState.CanCompileMap &&
            SdkHost.Actions?.BaseMaps != null && BaseMapAuthoring.TryValidateSource(map, out _);
    }
}
