using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_grease_trap")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Grease Trap")]
    public sealed class GreaseTrapMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.greasetrap";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
