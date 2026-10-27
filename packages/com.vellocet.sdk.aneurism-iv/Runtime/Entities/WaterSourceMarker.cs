using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_water_source")]
    [EntityAuthoring("Food / Supply", EntityPlacementKind.Point, "Water Source")]
    public sealed class WaterSourceMarker : ContactOperationMarker
    {
        public enum Kind { Sink, Fountain, CultFountain, WaterSourceBase }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.water-source." + kind.ToString().ToLowerInvariant();
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("effect_applied", EntitySignalValueType.String);
        }
    }
}
