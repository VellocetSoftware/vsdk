using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_foundry")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Foundry")]
    public sealed class FoundryMarker : OperationEntityMarker
    {
        [SerializeField] private OperationVolumeMarker fuelVolume;
        [SerializeField] private EntityMarker cruciblePivot;
        [SerializeField] private EntityMarker wheelPivot;
        public OperationVolumeMarker FuelVolume => fuelVolume;
        public EntityMarker CruciblePivot => cruciblePivot;
        public EntityMarker WheelPivot => wheelPivot;

        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.foundry";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("cast", EntitySignalValueType.None);
            builder.AddInput("toggle_pour", EntitySignalValueType.None);
            builder.AddOutput("casting_started", EntitySignalValueType.None);
            builder.AddOutput("casting_completed", EntitySignalValueType.None);
            builder.AddOutput("cast_completed", EntitySignalValueType.None);
            builder.AddOutput("processed", EntitySignalValueType.None);
            builder.AddOutput("fuel_added", EntitySignalValueType.Int);
            builder.AddOutput("metal_added", EntitySignalValueType.Int);
            builder.AddOutput("fuel_depleted", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_processing", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_pouring", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("metal_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("fuel_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("ingot_count", EntitySignalValueType.Int, true, false);
        }
    }
}
