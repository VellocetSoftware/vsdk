using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_lathe")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Lathe")]
    public sealed class LatheMarker : PoweredMachineMarker
    {
        [SerializeField, Min(0)] private float drainScale = 0.5f;
        public override float DrainScale => drainScale;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.lathe";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            base.ConfigureSignalContract(builder);
            builder.AddInput("collect", EntitySignalValueType.None);
            builder.AddOutput("ingredient_added", EntitySignalValueType.None);
            builder.AddOutput("ready", EntitySignalValueType.None);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddProperty("progress", EntitySignalValueType.Float, true, false);
            builder.AddProperty("has_input", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("has_output", EntitySignalValueType.Bool, true, false);
        }
    }
}
