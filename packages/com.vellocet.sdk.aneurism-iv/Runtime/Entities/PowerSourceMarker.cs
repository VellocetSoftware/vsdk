using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_power_source")]
    [EntityAuthoring("Infrastructure", EntityPlacementKind.Point, "Generator")]
    public sealed class PowerSourceMarker : OperationEntityMarker
    {
        public override string ArchetypeId => "aniv.power-source";
        public override string[] Outputs => new[] { "OnPowered", "OnDepleted" };

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("OnPowered", EntitySignalValueType.None);
            builder.AddOutput("OnDepleted", EntitySignalValueType.None);
            builder.AddOutput("refuelled", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("power_level", EntitySignalValueType.Float, true, false);
            builder.AddProperty("has_power", EntitySignalValueType.Bool, true, false);
        }
    }
}
