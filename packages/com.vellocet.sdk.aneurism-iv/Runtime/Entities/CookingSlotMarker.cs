using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_cooking_slot")]
    [EntityAuthoring("Food / Cooking", EntityPlacementKind.Point, "Cooking Slot")]
    public sealed class CookingSlotMarker : OperationEntityMarker
    {
        [SerializeField, Tooltip("Recipe station served by this independently placed cooking slot.")]
        private KitchenMarker station;
        public KitchenMarker Station => station;
        public override string ArchetypeId => "aniv.cooking-slot";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("collect", EntitySignalValueType.None);
            builder.AddOutput("ready", EntitySignalValueType.String);
            builder.AddOutput("burnt", EntitySignalValueType.String);
            builder.AddOutput("completed", EntitySignalValueType.String);
            builder.AddOutput("cancelled", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("phase", EntitySignalValueType.String, readable: true, writable: false);
            builder.AddProperty("progress", EntitySignalValueType.Float, readable: true, writable: false);
        }
    }
}
