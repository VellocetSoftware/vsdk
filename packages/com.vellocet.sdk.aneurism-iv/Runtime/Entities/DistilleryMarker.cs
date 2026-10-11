using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_distillery")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Distillery")]
    public sealed class DistilleryMarker : PoweredMachineMarker
    {
        [SerializeField, Min(0)] private float drainScale = 0.15f;
        public override float DrainScale => drainScale;
        public override string ArchetypeId => "aniv.distillery";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
