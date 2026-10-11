using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_smelter")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Smelter")]
    public sealed class SmelterMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.smelter";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
