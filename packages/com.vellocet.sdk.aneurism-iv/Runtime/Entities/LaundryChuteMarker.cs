using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_laundry_chute")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Laundry Chute")]
    public sealed class LaundryChuteMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.laundry-chute";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
