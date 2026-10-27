using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_fuel_refinery")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Fuel Refinery")]
    public sealed class FuelRefineryMarker : OperationEntityMarker
    {
        [SerializeField, Tooltip("Independent can-filling volume. Input Volume receives raw materials.")]
        private OperationVolumeMarker spoutVolume;
        public OperationVolumeMarker SpoutVolume => spoutVolume;
        public override string ArchetypeId => "aniv.fuel-refinery";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("ingredient_added", EntitySignalValueType.None);
            builder.AddOutput("ready", EntitySignalValueType.None);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("fuel_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("raw_count", EntitySignalValueType.Int, true, false);
        }
    }
}
