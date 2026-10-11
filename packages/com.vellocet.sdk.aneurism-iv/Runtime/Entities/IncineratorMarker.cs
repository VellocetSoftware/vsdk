using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_incinerator")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Incinerator")]
    public sealed class IncineratorMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.incinerator";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
