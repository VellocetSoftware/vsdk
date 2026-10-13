using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Aneurism
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public sealed class AneurismSdk : SdkGameDefinition
    {
        public const string GameId = "com.vellocet.aneurism-iv";
        static AneurismSdk() => Register();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Register() => SdkGame.Register(new AneurismSdk());

        public override string Id => GameId;
        public override string AudioContentTypeId => "stereoform.event";
        public override string AudioRuntimeCapability => "stereoform/2";
        public override string DisplayName => "ANEURISM IV";
        public override string DefaultModeId => "aneurism";
        public override bool SupportsExternalMods => false;
        public override string DocumentationSection => "ANEURISM_IV";
        public override IReadOnlyList<Vertex.Contracts.GlobalSignalDefinition> AdditionalGlobalEvents => GlobalEvents;
        private static readonly Vertex.Contracts.GlobalSignalDefinition[] GlobalEvents =
        {
            new("game", "event_changed", new Vertex.Contracts.EntitySignalParameter("event", "string"))
        };
        public override IReadOnlyList<ModeDescriptor> Modes => GameModes;
        public override SdkAssetRootMapping[] AssetRoots => Roots;
        public override int NavigationMask => ~LayerMask.GetMask("EntitySolid", "EntityClip", "RaycastOnly", "Passthrough");

        public override bool AllowsComponent(string typeName, string assemblyName) =>
            assemblyName == "Vellocet.Sdk" && typeName is "Entities.MapObjects.LightStyle" or "UI.HUD.PointOfInterest" or
                "Entities.MapObjects.DoorModel" or "Vellocet.Sdk.Aneurism.Entities.PrefabEntityPart" ||
            assemblyName == "Vellocet.Stereoform" && typeName == "Vellocet.Stereoform.AudioEmitter";

        public override bool AllowsSourceAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) path = path[..^5];
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension.Length == 0) return true;
            if (path.StartsWith("Assets/MapAuthoring/Kits/", StringComparison.Ordinal) && extension is ".prefab" or ".asset")
                return true;
            if (path.StartsWith("Assets/Audio/", StringComparison.Ordinal) && extension == ".asset") return true;
            if ((path.StartsWith("Assets/ScriptableObjects/LightStyles/", StringComparison.Ordinal) ||
                 path.StartsWith("Assets/ScriptableObjects/PointOfInterest/", StringComparison.Ordinal) ||
                 path.StartsWith("Assets/ScriptableObjects/Entities/Environment/Doors/", StringComparison.Ordinal)) && extension == ".asset")
                return true;
            return extension is ".fbx" or ".obj" or ".mat" or ".png" or ".jpg" or ".jpeg" or ".tga" or
                ".psd" or ".tif" or ".tiff" or ".exr" or ".hdr" or ".cubemap" or ".shader" or ".shadergraph" or
                ".shadersubgraph" or ".hlsl" or ".cginc" or ".compute" or ".anim" or ".controller" or ".overridecontroller" or ".mask" or ".ttf" or ".otf";
        }

        public override int ResolveLayers(SdkLayerSet layers)
        {
            var mask = 0;
            foreach (var (flag, names) in Layers)
                if ((layers & flag) != 0) mask |= LayerMask.GetMask(names);
            return mask;
        }

        private static readonly (SdkLayerSet Flag, string[] Names)[] Layers = {
            (SdkLayerSet.Pawn, new[] { "Player", "NetworkPlayer" }),
            (SdkLayerSet.Hitbox, new[] { "Bones", "LocalBones" }),
            (SdkLayerSet.Item, new[] { "Item" }),
            (SdkLayerSet.Default, new[] { "Default" }),
            (SdkLayerSet.Shield, new[] { "Entity" }),
            (SdkLayerSet.FirstPerson, new[] { "Introspection" }),
            (SdkLayerSet.Trigger, new[] { "Trigger" }),
            (SdkLayerSet.Ragdoll, new[] { "Bones", "LocalBones" })
        };

        private static readonly ModeDescriptor[] GameModes = {
            new("aneurism", "ANEURISM IV", "mode.aneurism", "default", "persistent-world"),
            new("gungame", "Gun Game", "mode.gungame", "GunGame"),
            new("nightmare", "Nightmare", "mode.nightmare", "Nightmare")
        };

        private static readonly SdkAssetRootMapping[] Roots = {
            new("Assets/Art/Materials", Constants.SdkManagedContentRoot + "/Materials"),
            new("Assets/Art/Models", Constants.SdkManagedContentRoot + "/Models"),
            new("Assets/Art/Textures", Constants.SdkManagedContentRoot + "/Textures"),
            new("Assets/Art/Shaders", Constants.SdkManagedContentRoot + "/Shaders"),
            new("Assets/Audio", Constants.SdkManagedContentRoot + "/Audio"),
            new("Assets/MapAuthoring/Kits", Constants.SdkEntityKitsRoot),
            new("Assets/ScriptableObjects/LightStyles", Constants.SdkManagedContentRoot + "/LightStyles"),
            new("Assets/ScriptableObjects/PointOfInterest", Constants.SdkManagedContentRoot + "/PointOfInterest"),
            new("Assets/ScriptableObjects/Entities/Environment/Doors", Constants.SdkManagedContentRoot + "/DoorModels")
        };
    }
}
