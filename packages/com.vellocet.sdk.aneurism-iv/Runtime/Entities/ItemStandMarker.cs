using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_item_stand")]
    [EntityAuthoring("Commerce", EntityPlacementKind.Point, "Item Stand")]
    public sealed class ItemStandMarker : ContactOperationMarker
    {
        [SerializeField] private PaystationMarker paystation;
        public PaystationMarker Paystation => paystation;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.item-stand";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            GameOperationContracts.Utility(builder);
        }
    }
}
