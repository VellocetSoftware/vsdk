using System;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_capitron_rack")]
    [EntityAuthoring("Industry / Capitron", EntityPlacementKind.Point, "Capitron Rack")]
    public sealed class RackMarker : ContactOperationMarker
    {
        [UnityEngine.SerializeField] private CapitronMarker[] slots = Array.Empty<CapitronMarker>();
        [UnityEngine.SerializeField] private bool useBuiltInSlots = true;
        public CapitronMarker[] Slots => slots;
        public bool UseBuiltInSlots => useBuiltInSlots;

        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.capitron-rack";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("installed", EntitySignalValueType.Int);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("slot_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("installed_count", EntitySignalValueType.Int, true, false);
        }
    }
}
