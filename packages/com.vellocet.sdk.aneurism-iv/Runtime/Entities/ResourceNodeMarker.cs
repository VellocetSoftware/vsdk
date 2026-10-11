using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_resource_node")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Resource Node")]
    public sealed class ResourceNodeMarker : PrefabEntityMarker
    {
        public enum Kind { CoalNode, OreNode, ResourceNode, OreNodeRigid, CoalNodeRigid }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override string ArchetypeId => "aniv.resource." + kind.ToString().ToLowerInvariant();
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
