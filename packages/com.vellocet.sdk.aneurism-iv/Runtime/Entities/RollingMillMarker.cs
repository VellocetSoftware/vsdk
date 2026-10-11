using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_rolling_mill")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Rolling Mill")]
    public sealed class RollingMillMarker : PoweredMachineMarker
    {
        [SerializeField, Min(0)] private float drainScale = 0.05f;
        public override float DrainScale => drainScale;
        public override string ArchetypeId => "aniv.rolling-mill";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
