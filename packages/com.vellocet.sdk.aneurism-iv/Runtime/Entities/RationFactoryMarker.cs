using System;
using UnityEngine;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_ration_factory")]
    [EntityAuthoring("Food / Rations", EntityPlacementKind.Point, "Ration Factory")]
    public sealed class RationFactoryMarker : ContactOperationMarker
    {
        [SerializeField] private string tier1Resource = GameItem.Fish;
        [SerializeField] private string tier2Resource = GameItem.SuspiciousMeat;
        public string Tier1Resource => tier1Resource;
        public string Tier2Resource => tier2Resource;
        public override string ArchetypeId => "aniv.ration-factory";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("supply_added", EntitySignalValueType.None);
            builder.AddOutput("item_ground", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("stock", EntitySignalValueType.Int, true, false);
            builder.AddProperty("ration_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("production_enabled", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("required_item", EntitySignalValueType.String, true, false);
        }
    }
}
