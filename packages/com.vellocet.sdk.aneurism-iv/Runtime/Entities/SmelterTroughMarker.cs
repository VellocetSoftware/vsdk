using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_smelter_trough")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Smelter Trough")]
    public sealed class SmelterTroughMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.smelter-trough";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
