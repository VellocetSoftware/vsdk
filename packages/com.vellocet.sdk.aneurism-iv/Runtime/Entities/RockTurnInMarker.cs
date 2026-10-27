using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_rock_turn_in")]
    [EntityAuthoring("Industry / Resources", EntityPlacementKind.Volume, "Rock Turn-In Trigger", Description = "Independent rock delivery volume. Accepts rocks and rewards their last pusher after retirement.")]
    public sealed class RockTurnInMarker : EntityMarker, IBoundedEntityMarker
    {
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("accepted", EntitySignalValueType.None);
        }
    }
}
