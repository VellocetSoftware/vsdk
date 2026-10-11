using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_ration_station")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Ration Station")]
    public sealed class RationStationMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.ration-station";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
