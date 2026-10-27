using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_wardrobe")]
    [EntityAuthoring("Services", EntityPlacementKind.Point, "Wardrobe")]
    public sealed class WardrobeMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.wardrobe";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddOutput("equipped", EntitySignalValueType.Int);
        }
    }
}
