using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_boulder")]
    [EntityAuthoring("Industry / Resources", EntityPlacementKind.Point, "Boulder")]
    public sealed class BoulderMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.boulder";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("push", EntitySignalValueType.None);
            builder.AddInput("pull", EntitySignalValueType.None);
            builder.AddOutput("pushed", EntitySignalValueType.None);
            builder.AddOutput("pulled", EntitySignalValueType.None);
        }
    }
}
