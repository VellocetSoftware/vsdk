using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_laundry_turn_in")]
    [EntityAuthoring("Advanced / Processing", EntityPlacementKind.Volume, "Laundry Turn-in")]
    public sealed class LaundryTurnInMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField, Tooltip("Accept eligible dropped items in this volume automatically.")]
        private bool automatic = true;
        public bool Automatic => automatic;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("accept", EntitySignalValueType.None);
            builder.AddOutput("accepted", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
        }
    }
}
