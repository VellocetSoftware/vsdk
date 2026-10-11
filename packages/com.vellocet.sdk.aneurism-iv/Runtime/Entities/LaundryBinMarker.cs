using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_laundry_bin")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Laundry Bin")]
    public sealed class LaundryBinMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.laundry-bin";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
