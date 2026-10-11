using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_boulder")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Boulder")]
    public sealed class BoulderMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.boulder";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
