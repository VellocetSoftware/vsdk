using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_terminal")]
    [EntityAuthoring("Information", EntityPlacementKind.Point, "Terminal")]
    public sealed class TerminalMarker : ContactOperationMarker
    {
        [SerializeField] private PropertyMarker property;
        public PropertyMarker Property => property;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.terminal";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("session_opened", EntitySignalValueType.String);
            builder.AddOutput("session_closed", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("in_use", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("app", EntitySignalValueType.String, true, false);
        }
    }
}
