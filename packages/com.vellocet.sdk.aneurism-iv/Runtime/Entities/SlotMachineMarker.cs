using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_slot_machine")]
    [EntityAuthoring("Commerce", EntityPlacementKind.Point, "Slot Machine")]
    public sealed class SlotMachineMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.slot-machine";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("spin", EntitySignalValueType.None);
            builder.AddOutput("started", EntitySignalValueType.None);
            builder.AddOutput("settled", EntitySignalValueType.Int);
            builder.AddOutput("completed", EntitySignalValueType.Int);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_spinning", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("pot", EntitySignalValueType.Int, true, false);
            builder.AddProperty("spin_cost", EntitySignalValueType.Int, true, false);
            builder.AddProperty("result", EntitySignalValueType.String, true, false);
        }
    }
}
