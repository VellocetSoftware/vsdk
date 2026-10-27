using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_smelter_trough")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Smelter Trough")]
    public sealed class SmelterTroughMarker : OperationEntityMarker
    {
        public override string ArchetypeId => "aniv.smelter-trough";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
        }
    }
}
