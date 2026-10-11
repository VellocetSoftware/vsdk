using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Entities
{
    [EntityClass(EntityId)]
    [EntityAuthoring("Map", EntityPlacementKind.Point, "Map Settings")]
    [DisallowMultipleComponent]
    public sealed class MapSettingsMarker : EntityMarker
    {
        public const string EntityId = "info_map_settings";

        [SerializeField, Tooltip("Optional. Override the score used during loading and gameplay. Unassigned uses the game's default score.")]
        private MapMusicSet musicSet;

        public MapMusicSet MusicSet => musicSet;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
