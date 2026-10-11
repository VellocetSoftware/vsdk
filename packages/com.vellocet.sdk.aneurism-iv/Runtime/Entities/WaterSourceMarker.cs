using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_water_source")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Water Source")]
    public sealed class WaterSourceMarker : PrefabEntityMarker
    {
        public enum Kind { Sink, Fountain, CultFountain, WaterSourceBase }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override string ArchetypeId => "aniv.water-source." + kind.ToString().ToLowerInvariant();
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
