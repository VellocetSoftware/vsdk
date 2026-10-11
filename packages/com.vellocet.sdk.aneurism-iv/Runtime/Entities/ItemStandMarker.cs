using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_item_stand")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Item Stand")]
    public sealed class ItemStandMarker : PrefabEntityMarker
    {
        [SerializeField] private PaystationMarker paystation;
        public PaystationMarker Paystation => paystation;
        public override string ArchetypeId => "aniv.item-stand";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
