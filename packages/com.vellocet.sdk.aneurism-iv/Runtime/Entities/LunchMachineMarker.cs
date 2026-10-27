using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_lunch_machine")]
    [EntityAuthoring("Food / Rations", EntityPlacementKind.Point, "Lunch Machine")]
    public sealed class LunchMachineMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.lunch-machine";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("plus", EntitySignalValueType.None);
            builder.AddInput("minus", EntitySignalValueType.None);
            builder.AddInput("clear", EntitySignalValueType.None);
            builder.AddInput("enter", EntitySignalValueType.None);
            builder.AddOutput("answer_changed", EntitySignalValueType.Int);
            builder.AddOutput("challenge_changed", EntitySignalValueType.String);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddOutput("failed", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("question", EntitySignalValueType.String, true, false);
            builder.AddProperty("answer", EntitySignalValueType.Int, true, false);
            builder.AddProperty("is_available", EntitySignalValueType.Bool, true, false);
        }
    }
}
