using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_incinerator")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Point, "Incinerator")]
    public sealed class IncineratorMarker : ContactOperationMarker
    {
        public override string ArchetypeId => "aniv.incinerator";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("completed", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("body_count", EntitySignalValueType.Int, true, false);
        }
    }
}
