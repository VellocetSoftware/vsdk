using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_pharma_machine")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Pharmaceutical Machine")]
    public sealed class PharmaMachineMarker : OperationEntityMarker
    {
        public enum Kind { AnalepticalCompounder, AntiradiativeEncapsulator, HemobaseSeparator, LucidialTonicAmpoules, ParaceuticalReagentTank }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("start", EntitySignalValueType.String);
            builder.AddInput("add", EntitySignalValueType.None);
            builder.AddInput("collect", EntitySignalValueType.None);
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddOutput("ingredients_added", EntitySignalValueType.None);
            builder.AddOutput("started", EntitySignalValueType.String);
            builder.AddOutput("ready", EntitySignalValueType.String);
            builder.AddOutput("expired", EntitySignalValueType.String);
            builder.AddOutput("completed", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("phase", EntitySignalValueType.String, readable: true, writable: false);
            builder.AddProperty("busy", EntitySignalValueType.Bool, readable: true, writable: false);
            builder.AddProperty("progress", EntitySignalValueType.Float, readable: true, writable: false);
        }
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.pharma-machine." + kind.ToString().ToLowerInvariant();
    }
}
