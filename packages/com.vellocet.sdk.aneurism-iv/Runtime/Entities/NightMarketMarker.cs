using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_night_market")]
    [EntityAuthoring("Commerce", EntityPlacementKind.Point, "Night Market")]
    public sealed class NightMarketMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.night-market";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddInput("purchase", EntitySignalValueType.String);
            builder.AddInput("call_merchants", EntitySignalValueType.None);
            builder.AddOutput("purchased", EntitySignalValueType.Int);
            builder.AddOutput("merchants_called", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
        }
    }
}
