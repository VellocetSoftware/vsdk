using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_grease_trap")]
    [EntityAuthoring("Food / Cooking", EntityPlacementKind.Point, "Grease Trap")]
    public sealed class GreaseTrapMarker : ContactOperationMarker
    {
        [SerializeField] private EntityMarker reservoirVisual;
        public EntityMarker ReservoirVisual => reservoirVisual;

        public override string ArchetypeId => "aniv.greasetrap";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("enable", EntitySignalValueType.None);
            builder.AddInput("disable", EntitySignalValueType.None);
            builder.AddOutput("started", EntitySignalValueType.None);
            builder.AddOutput("stopped", EntitySignalValueType.None);
            builder.AddOutput("filled", EntitySignalValueType.None);
            builder.AddOutput("depleted", EntitySignalValueType.None);
            builder.AddProperty("quantity", EntitySignalValueType.Float, true, false);
            builder.AddProperty("is_siphoning", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
        }
    }
}
