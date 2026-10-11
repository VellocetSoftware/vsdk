using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_kitchen")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Kitchen")]
    public sealed class KitchenMarker : PrefabEntityMarker
    {
        public enum Kind { Kitchen, BarrelGrill }
        [SerializeField] private Kind kind;
        public Kind KitchenKind => kind;
        public override string ArchetypeId => kind switch {
            Kind.Kitchen => "aniv.kitchen", Kind.BarrelGrill => "aniv.barrel-grill", _ => null
        };
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
