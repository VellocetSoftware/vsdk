using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_power_source")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Generator")]
    public sealed class PowerSourceMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.power-source";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
