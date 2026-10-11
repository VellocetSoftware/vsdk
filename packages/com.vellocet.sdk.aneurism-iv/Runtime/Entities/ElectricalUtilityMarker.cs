using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_electrical_utility")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Electrical Utility")]
    public sealed class ElectricalUtilityMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.electrical-utility";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
