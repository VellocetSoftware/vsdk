using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_vent")]
    [EntityAuthoring("Infrastructure", EntityPlacementKind.Point, "Vent")]
    public sealed class VentMarker : OperationEntityMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.vent";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("request_cash", EntitySignalValueType.None);
            builder.AddOutput("cash_dispensed", EntitySignalValueType.None);
            builder.AddOutput("paid", EntitySignalValueType.Int);
        }
    }
}
