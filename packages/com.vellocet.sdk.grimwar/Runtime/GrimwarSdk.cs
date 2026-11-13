using System.Collections.Generic;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif
using Vellocet.Sdk.Entities.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Grimwar
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public sealed class GrimwarSdk : SdkGameDefinition
    {
#if UNITY_5_3_OR_NEWER
        static GrimwarSdk() => Register();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Register() => SdkGame.Register(new GrimwarSdk());
#endif

        public override string MinimumMapSdkVersion => "4.0.0";
        public override string AudioRuntimeCapability => "wwise";
        public override string Id => "com.vellocet.grimwar";
        public override string DisplayName => "Grimwar";
        public override string DefaultModeId => ModeCatalog.KothId;
        public override float NavigationAgentRadius => 0.4375f;
        public override float NavigationAgentHeight => 1.75f;
        public override string FallbackModeId => ModeCatalog.SandboxId;
        public override Maps.MapMusicSet DefaultMusicSet => Maps.MapMusicSet.Ordainment;
        public override string DocumentationSection => "Grimwar";
        public override bool SupportsExternalMods => true;
        public override string AudioContentTypeId => Addons.AddonMedia.LegacyGrimwarAudioType;
        public override IReadOnlyList<Vertex.Contracts.GlobalSignalDefinition> AdditionalGlobalEvents => GlobalEvents;
        private static readonly Vertex.Contracts.GlobalSignalDefinition[] GlobalEvents = {
            new(VSig.VSigGlobalEvents.Gravewar.Source, VSig.VSigGlobalEvents.Gravewar.WaveStarted,
                new Vertex.Contracts.EntitySignalParameter("wave", "int"),
                new Vertex.Contracts.EntitySignalParameter("zombies", "int")),
            new(VSig.VSigGlobalEvents.Gravewar.Source, VSig.VSigGlobalEvents.Gravewar.WaveEnded,
                new Vertex.Contracts.EntitySignalParameter("wave", "int"),
                new Vertex.Contracts.EntitySignalParameter("result", "string"))
        };
        public override bool RequiresPackageGameId => false; // Released Grimwar packages predate the shared SDK.
#if UNITY_5_3_OR_NEWER
        public override int NavigationMask => ~LayerMask.GetMask("EntitySolid", "EntityClip", "RaycastOnly");
        public override int ResolveLayers(SdkLayerSet layers)
        {
            var mask = 0;
            foreach (var (flag, name) in Layers)
                if ((layers & flag) != 0) mask |= LayerMask.GetMask(name);
            return mask;
        }
        private static readonly (SdkLayerSet Flag, string Name)[] Layers = {
            (SdkLayerSet.Pawn, "Pawn"), (SdkLayerSet.Hitbox, "Hitbox"),
            (SdkLayerSet.Item, "Item"), (SdkLayerSet.Default, "Default"),
            (SdkLayerSet.Shield, "Shield"), (SdkLayerSet.FirstPerson, "FirstPerson"),
            (SdkLayerSet.Trigger, "Trigger"), (SdkLayerSet.Ragdoll, "Ragdoll")
        };
#else
        public override int NavigationMask => throw new System.PlatformNotSupportedException("Layer masks require Unity.");
        public override int ResolveLayers(SdkLayerSet layers) =>
            throw new System.PlatformNotSupportedException("Layer masks require Unity.");
#endif
        public override IReadOnlyList<ModeDescriptor> Modes => GameModes;
        public override SdkAssetRootMapping[] AssetRoots => Roots;
        private static readonly string[] Assemblies = { "Vellocet.Sdk", "Grimwar.Game.Entities" };
        public override IReadOnlyList<string> MarkerAssemblies => Assemblies;
        public override bool AllowsComponent(string typeName, string assemblyName) =>
            typeName == "Grimwar.Game.Runtime.Entities.Shared.Effects.EffectController" && assemblyName == "Grimwar.Game.Entities";

        private static readonly ModeDescriptor[] GameModes = {
            new(ModeCatalog.KothId, "King Of The Hill", "mode.koth", ModeCatalog.KothId, "king_of_the_hill", "kingofthehill"),
            new(ModeCatalog.AssaultId, "Assault", "mode.assault", ModeCatalog.AssaultId, "assault"),
            new(ModeCatalog.BlitzId, "Blitz", "mode.blitz", "bz", "payload"),
            new(ModeCatalog.UplinkId, "Uplink", "mode.uplink", "up"),
            new(ModeCatalog.ArenaId, "Arena", "mode.arena", ModeCatalog.ArenaId, "ar"),
            new(ModeCatalog.SandboxId, "Sandbox", "mode.sandbox", "sb"),
            new(ModeCatalog.GravewarId, "Gravewar", "mode.gravewar", "gr")
        };
        private static readonly SdkAssetRootMapping[] Roots = {
            new("Assets/Art/Materials/Decals", Constants.SdkManagedContentRoot + "/Materials/Decals"),
            new("Assets/Art/Materials/Shaders", Constants.SdkManagedContentRoot + "/Materials/Shaders"),
            new("Assets/Art/Materials/Modular", Constants.SdkManagedContentRoot + "/Materials/Modular"),
            new("Assets/Art/Materials/Templates", Constants.SdkManagedContentRoot + "/Materials/Templates"),
            new(Constants.SharedMaterialsEntitiesRoot, Constants.SdkMaterialsEntitiesRoot),
            new(Constants.SharedMaterialsEnvironmentRoot, Constants.SdkMaterialsEnvironmentRoot),
            new(Constants.SharedMaterialsPropsRoot, Constants.SdkMaterialsPropsRoot),
            new(Constants.SharedModelsEntitiesRoot, Constants.SdkModelsEntitiesRoot),
            new(Constants.SharedModelsModularRoot, Constants.SdkModelsModularRoot),
            new(Constants.SharedModelsPropsRoot, Constants.SdkModelsPropsRoot),
            new("Assets/Art/Textures/Decals", Constants.SdkManagedContentRoot + "/Textures/Decals"),
            new("Assets/Art/Textures/Hatching", Constants.SdkManagedContentRoot + "/Textures/Hatching"),
            new("Assets/Art/Textures/Modular", Constants.SdkManagedContentRoot + "/Textures/Modular"),
            new("Assets/Art/Textures/VFX/blood", Constants.SdkManagedContentRoot + "/Textures/VFX/blood"),
            new(Constants.SharedTexturesEntitiesRoot, Constants.SdkTexturesEntitiesRoot),
            new(Constants.SharedTexturesEnvironmentRoot, Constants.SdkTexturesEnvironmentRoot),
            new(Constants.SharedTexturesPropsRoot, Constants.SdkTexturesPropsRoot),
            new(Constants.SharedShadersSkyRoot, Constants.SdkShadersSkyRoot),
            new("Assets/Art/VFX/Shaders/Decals", Constants.SdkManagedContentRoot + "/Shaders/Decals"),
            new(Constants.SharedEntityKitsRoot, Constants.SdkEntityKitsRoot)
        };
    }
}
