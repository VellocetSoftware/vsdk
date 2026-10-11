using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_fuel_refinery")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Fuel Refinery")]
    public sealed class FuelRefineryMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.fuel-refinery";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
