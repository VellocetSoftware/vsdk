using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_smelter")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Smelter")]
    public sealed class SmelterMarker : ContactOperationMarker
    {
        public override string ArchetypeId => "aniv.smelter";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("fuel_added", EntitySignalValueType.None);
            builder.AddOutput("fuel_depleted", EntitySignalValueType.None);
            builder.AddOutput("heated", EntitySignalValueType.None);
            builder.AddOutput("cooled", EntitySignalValueType.None);
            builder.AddProperty("heat", EntitySignalValueType.Float, true, false);
            builder.AddProperty("fuel_seconds", EntitySignalValueType.Float, true, false);
            builder.AddProperty("is_heated", EntitySignalValueType.Bool, true, false);
        }
    }
}
