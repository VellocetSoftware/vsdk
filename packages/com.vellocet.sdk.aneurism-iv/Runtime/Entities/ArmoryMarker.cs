using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_armory")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Armory")]
    public sealed class ArmoryMarker : PrefabEntityMarker
    {
        public enum Kind { Armory, UndergroundArmory }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override string ArchetypeId => "aniv.armory." + kind.ToString().ToLowerInvariant();
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
