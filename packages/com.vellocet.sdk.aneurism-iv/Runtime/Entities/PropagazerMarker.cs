using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_propagazer")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Propagazer")]
    public sealed class PropagazerMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.propagazer";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
