using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_evil_one")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Evil One")]
    public sealed class EvilOneMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.evil-one";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
