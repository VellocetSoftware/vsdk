using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_resource_node")]
    [EntityAuthoring("Industry / Resources", EntityPlacementKind.Point, "Resource Node")]
    public sealed class ResourceNodeMarker : OperationEntityMarker
    {
        public enum Kind { CoalNode, OreNode, ResourceNode, OreNodeRigid, CoalNodeRigid }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override string ArchetypeId => "aniv.resource." + kind.ToString().ToLowerInvariant();
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("broken", EntitySignalValueType.None);
            builder.AddOutput("restored", EntitySignalValueType.None);
            builder.AddOutput("produced", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_broken", EntitySignalValueType.Bool, true, false);
        }
    }
}
