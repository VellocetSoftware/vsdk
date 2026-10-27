using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_dealer_pickup")]
    [EntityAuthoring("Commerce", EntityPlacementKind.Point, "Dealer Pickup")]
    public sealed class DealerPickupMarker : WaypointPrefabMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.dealer-pickup";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("collect", EntitySignalValueType.None);
            builder.AddOutput("collected", EntitySignalValueType.Int);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("busy", EntitySignalValueType.Bool, true, false);
        }
    }
}
