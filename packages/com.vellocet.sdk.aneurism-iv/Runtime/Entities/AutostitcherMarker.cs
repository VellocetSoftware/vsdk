using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_autostitcher")]
    [EntityAuthoring("Services", EntityPlacementKind.Point, "Autostitcher")]
    public sealed class AutostitcherMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.autostitcher";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("heal", EntitySignalValueType.None);
            builder.AddInput("refill", EntitySignalValueType.None);
            builder.AddOutput("started", EntitySignalValueType.None);
            builder.AddOutput("healing_applied", EntitySignalValueType.None);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddOutput("cancelled", EntitySignalValueType.None);
            builder.AddOutput("refilled", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("busy", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("charges", EntitySignalValueType.Int, true, false);
            builder.AddProperty("price", EntitySignalValueType.Float, true, false);
        }
    }
}
