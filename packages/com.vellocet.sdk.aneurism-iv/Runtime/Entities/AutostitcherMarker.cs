using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_autostitcher")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Autostitcher")]
    public sealed class AutostitcherMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.autostitcher";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
