using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Entities
{
    [EntityClass(EntityId)]
    [EntityAuthoring("Map setup", EntityPlacementKind.Point, "Map Settings")]
    [DisallowMultipleComponent]
    public sealed class MapSettingsMarker : EntityMarker
    {
        public const string EntityId = "info_map_settings";

        [SerializeField, Tooltip("Optional. Override the score used during loading and gameplay. Unassigned uses the game's default score.")]
        private MapMusicSet musicSet;

        [SerializeField, Tooltip("Override the bounds baked from map geometry. The volume is centered on this entity.")]
        private bool overridePlayableBounds;
        [SerializeField, Tooltip("World size in metres. Cleanup adds a 64 metre safety margin.")]
        private Vector3 playableBoundsSize = new(1000, 1000, 1000);
        public bool OverridesPlayableBounds => overridePlayableBounds;
        public Bounds PlayableBounds => new(transform.position, playableBoundsSize);

        public MapMusicSet MusicSet => musicSet;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
