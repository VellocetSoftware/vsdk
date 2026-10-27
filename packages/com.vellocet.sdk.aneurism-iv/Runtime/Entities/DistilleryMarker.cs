using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_distillery")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Distillery")]
    public sealed class DistilleryMarker : PoweredMachineMarker
    {
        [SerializeField, Min(0)] private float drainScale = 0.15f;
        public override float DrainScale => drainScale;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.distillery";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            base.ConfigureSignalContract(builder);
            builder.AddInput("bottle", EntitySignalValueType.None);
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddOutput("ingredient_added", EntitySignalValueType.None);
            builder.AddOutput("ready", EntitySignalValueType.None);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddProperty("temperature", EntitySignalValueType.Float, true, false);
            builder.AddProperty("ready_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("processing_count", EntitySignalValueType.Int, true, false);
        }
    }
}
