using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_neurostabilizer")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Neurostabilizer")]
    public sealed class NeurostabilizerMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.neurostabilizer";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
