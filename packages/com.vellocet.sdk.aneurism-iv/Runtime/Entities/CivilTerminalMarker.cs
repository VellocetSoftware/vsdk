using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_civil_terminal")]
    [EntityAuthoring("Information", EntityPlacementKind.Point, "Civil Terminal")]
    public sealed class CivilTerminalMarker : WaypointPrefabMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.civil-terminal";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddInput("redeem", EntitySignalValueType.None);
            builder.AddInput("pay_taxes", EntitySignalValueType.None);
            builder.AddOutput("redeemed", EntitySignalValueType.Int);
            builder.AddOutput("taxes_paid", EntitySignalValueType.Int);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            GameOperationContracts.Utility(builder);
        }
    }
}
