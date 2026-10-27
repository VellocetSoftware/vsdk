using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_metro_checkpoint")]
    [EntityAuthoring("Security", EntityPlacementKind.Point, "Metro Checkpoint")]
    public sealed class MetroCheckpointMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.metro-checkpoint";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("request_access", EntitySignalValueType.None);
            builder.AddOutput("access_changed", EntitySignalValueType.Bool);
            builder.AddOutput("trespass_detected", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_open", EntitySignalValueType.Bool, true, false);
        }
    }
}
