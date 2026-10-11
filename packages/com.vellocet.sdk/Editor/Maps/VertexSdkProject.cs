using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>The SDK publisher supplies this data; end users do not configure a Unity project.</summary>
    [InitializeOnLoad]
    public static class VertexSdkProject
    {
        public const string GamePath = "ProjectSettings/VertexGame.vertexgame";
        static VertexSdkProject()
        {
            if (!File.Exists(GamePath)) return;
            var game = GameProfile.Load(GamePath);
            VertexSdkRegistry.RegisterCompiler(game.Id, game.Version, Compile);
        }

        private static void Compile(VertexBuildContext context)
        {
            Prepare();
            VertexMapCompiler.Compile(context);
        }

        public static void Prepare()
        {
            if (!File.Exists(GamePath)) throw new InvalidDataException("This is not a published Vertex SDK compiler project.");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/VertexSdk/Rendering/Pipeline.asset");
            if (!pipeline) throw new InvalidDataException("The SDK is missing its published render pipeline. Reinstall the SDK.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var stripping = GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>();
            if (stripping == null) throw new InvalidDataException("The SDK compiler could not initialize URP shader settings.");
            // Published quality pipelines preserve the game's features, including layered decals and MSAA.
            stripping.stripUnusedVariants = true;
            EditorUtility.SetDirty(GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>());
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            SharedAssetAddressableEditorUtility.EnsureSdkSharedAssets(settings);
            // Shared script assets retain their original GUIDs and addresses in the game.
            var addresses = JsonUtility.FromJson<InstalledAddresses>(File.ReadAllText("ProjectSettings/VertexBaseAddresses.json"));
            var group = SharedAssetAddressableEditorUtility.EnsureSharedGroup(settings);
            foreach (var asset in addresses.assets)
            {
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(asset.guid))) throw new InvalidDataException("Missing installed base asset: " + asset.guid);
                var entry = settings.CreateOrMoveEntry(asset.guid, group, false, false);
                entry.address = asset.address;
            }
            AssetDatabase.SaveAssets();
        }

        [System.Serializable] public sealed class InstalledAddresses { public InstalledAddress[] assets; }
        [System.Serializable] public sealed class InstalledAddress { public string guid; public string address; }
    }
}
