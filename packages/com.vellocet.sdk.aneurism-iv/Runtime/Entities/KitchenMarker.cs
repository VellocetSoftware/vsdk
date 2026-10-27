using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_kitchen")]
    [EntityAuthoring("Food / Cooking", EntityPlacementKind.Point, "Kitchen")]
    public sealed class KitchenMarker : RecipeStationMarker
    {
        public enum Kind { Kitchen, BarrelGrill }
        [SerializeField] private Kind kind;
        [SerializeField, Tooltip("Use the supplied cooking slots when no independent Cooking Slot markers reference this station.")]
        private bool useBuiltInSlots = true;
        public bool UseBuiltInSlots => useBuiltInSlots;
        public Kind KitchenKind => kind;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            base.ConfigureSignalContract(builder);
            builder.AddInput("collect", EntitySignalValueType.None);
            builder.AddOutput("cooking_ready", EntitySignalValueType.String);
            builder.AddOutput("cooking_burnt", EntitySignalValueType.String);
            builder.AddOutput("cooking_cancelled", EntitySignalValueType.String);
        }
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => kind switch {
            Kind.Kitchen => "aniv.kitchen", Kind.BarrelGrill => "aniv.barrel-grill", _ => null
        };
    }
}
