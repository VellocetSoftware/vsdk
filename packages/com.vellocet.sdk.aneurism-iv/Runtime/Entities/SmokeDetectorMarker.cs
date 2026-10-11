using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_smoke_detector")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Smoke Detector")]
    public sealed class SmokeDetectorMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.smoke-detector";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
