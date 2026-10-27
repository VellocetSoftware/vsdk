using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_underground")]
    [EntityAuthoring("Triggers", EntityPlacementKind.Volume, "Underground Volume")]
    public sealed class UndergroundVolumeMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField, Min(0)] private float infectionAmount = 20;
        [SerializeField] private bool infectionEnabled = true;
        public float InfectionAmount => infectionAmount;
        public bool InfectionEnabled => infectionEnabled;
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
