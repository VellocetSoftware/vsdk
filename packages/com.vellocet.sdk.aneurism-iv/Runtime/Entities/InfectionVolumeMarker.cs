using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_infection")]
    [EntityAuthoring("Triggers", EntityPlacementKind.Volume, "Infection Volume")]
    public sealed class InfectionVolumeMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField, Min(0)] private float amount = 5;
        public float Amount => amount;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("enable", EntitySignalValueType.None);
            builder.AddInput("disable", EntitySignalValueType.None);
            builder.AddInput("scan", EntitySignalValueType.None);
            builder.AddOutput("enabled", EntitySignalValueType.None);
            builder.AddOutput("disabled", EntitySignalValueType.None);
            builder.AddOutput("infection_applied", EntitySignalValueType.Int);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_available", EntitySignalValueType.Bool, true, false);
        }
    }
}
