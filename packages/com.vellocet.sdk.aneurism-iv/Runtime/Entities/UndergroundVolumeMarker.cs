using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_aniv_underground")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Volume, "Underground Volume")]
    public sealed class UndergroundVolumeMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField, Min(0)] private float infectionAmount = 20;
        [SerializeField] private bool infectionEnabled = true;
        public float InfectionAmount => infectionAmount;
        public bool InfectionEnabled => infectionEnabled;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
