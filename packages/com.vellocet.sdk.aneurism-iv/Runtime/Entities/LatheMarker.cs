using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_lathe")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Lathe")]
    public sealed class LatheMarker : PoweredMachineMarker
    {
        [SerializeField, Min(0)] private float drainScale = 0.5f;
        public override float DrainScale => drainScale;
        public override string ArchetypeId => "aniv.lathe";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
