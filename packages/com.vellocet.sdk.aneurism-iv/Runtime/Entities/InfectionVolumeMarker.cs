using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_aniv_infection")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Volume, "Infection Volume")]
    public sealed class InfectionVolumeMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField, Min(0)] private float amount = 5;
        public float Amount => amount;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
