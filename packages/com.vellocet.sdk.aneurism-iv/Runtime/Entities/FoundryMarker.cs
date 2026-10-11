using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_foundry")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Foundry")]
    public sealed class FoundryMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.foundry";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
