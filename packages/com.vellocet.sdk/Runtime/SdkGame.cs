using System;
using System.Collections.Generic;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk
{
    public abstract class SdkGameDefinition
    {
        public virtual string MinimumMapSdkVersion => null;
        public abstract string Id { get; }
        public virtual Type DoorSettingsComponentType => null;
        public virtual Type DoorPropertyMarkerType => null;
        public abstract string DisplayName { get; }
        public abstract string DefaultModeId { get; }
        public virtual string FallbackModeId => DefaultModeId;
        public virtual Maps.MapMusicSet DefaultMusicSet => default;
        public abstract IReadOnlyList<ModeDescriptor> Modes { get; }
        public abstract SdkAssetRootMapping[] AssetRoots { get; }
        public abstract bool SupportsExternalMods { get; }
        public virtual bool RequiresPackageGameId => true;
        public virtual string AudioContentTypeId => "vellocet.audio";
        public virtual string AudioRuntimeCapability => "";
        public virtual IReadOnlyList<Vertex.Contracts.GlobalSignalDefinition> AdditionalGlobalEvents =>
            Array.Empty<Vertex.Contracts.GlobalSignalDefinition>();
        public abstract int ResolveLayers(SdkLayerSet layers);
        public abstract int NavigationMask { get; }
        // Installed game policy owns resolution and limits; map authors cannot raise them.
        public virtual Vellocet.Framework.Runtime.Spatial.SpatialOctreeBakeSettings SpatialBakeSettings =>
            Vellocet.Framework.Runtime.Spatial.SpatialOctreeBakeSettings.Default;
        public virtual string DocumentationSection => string.Empty;
        private static readonly string[] SharedMarkerAssemblies = { "Vellocet.Sdk" };
        public virtual IReadOnlyList<string> MarkerAssemblies => SharedMarkerAssemblies;
        public virtual bool AllowsSourceAsset(string path) => true;
        public virtual bool AllowsComponent(string typeName, string assemblyName) => false;
    }

    public static class SdkGame
    {
        private static SdkGameDefinition _current;
        public static SdkGameDefinition Current => _current ??
            throw new InvalidOperationException("Install a Vellocet game authoring package before using the SDK.");

        public static void Register(SdkGameDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
                string.IsNullOrWhiteSpace(definition.DefaultModeId))
                throw new ArgumentException("A game authoring package must declare its identity and default mode.");
            if (_current != null && !string.Equals(_current.Id, definition.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("Only one game authoring package may be installed in a compiler project.");
            _current = definition;
        }
    }
}
