using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_capitron")]
    [EntityAuthoring("Industry / Capitron", EntityPlacementKind.Point, "Capitron 3000")]
    public sealed class CapitronMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.capitron";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("collect", EntitySignalValueType.None);
            builder.AddOutput("collected", EntitySignalValueType.Int);
            builder.AddOutput("mounted_changed", EntitySignalValueType.Bool);
            builder.AddProperty("is_mounted", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("hunger", EntitySignalValueType.Float, true, false);
            builder.AddProperty("credits", EntitySignalValueType.Int, true, false);
            builder.AddProperty("busy", EntitySignalValueType.Bool, true, false);
        }
    }
}
