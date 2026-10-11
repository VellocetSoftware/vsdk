using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_aniv_spawn")]
    [EntityAuthoring("Players", EntityPlacementKind.Point, "Player Spawn")]
    public sealed class SpawnPointMarker : EntityMarker
    {
        public enum SpawnKind { Default = -1, SlaveCamp = 0, Underground = 1, VomitSpawn = 2, Cortex = 3 }
        [SerializeField] private SpawnKind kind = SpawnKind.Default;
        public SpawnKind Kind => kind;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
