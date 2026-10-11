using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_night_market")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Night Market")]
    public sealed class NightMarketMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.night-market";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
