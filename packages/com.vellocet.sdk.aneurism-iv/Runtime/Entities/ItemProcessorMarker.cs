using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_item_processor")]
    [EntityAuthoring("Advanced / Processing", EntityPlacementKind.Volume, "Item Processor", Description = "Advanced native conversion volume. Select a built-in processing profile and wire start/completed ports.")]
    public sealed class ItemProcessorMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField, Tooltip("Game-owned conversion profile. Duration, capacity and item rules come from the selected profile.")]
        [EntityStringChoices(ItemConversionId.WashLaundry, "Wash laundry", ItemConversionId.DryLaundry, "Dry laundry",
            ItemConversionId.FoldLaundry, "Fold laundry", ItemConversionId.FillSlopCan, "Fill slop can", ItemConversionId.MakeJenkem, "Make jenkem")]
        private string profile = ItemConversionId.WashLaundry;
        [SerializeField, Tooltip("Optional output point in this map. Defaults to the centre of the input volume.")]
        private EntityMarker outputTarget;

        public string Profile => profile;
        public EntityMarker OutputTarget => outputTarget;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("start", EntitySignalValueType.None);
            builder.AddInput("cancel", EntitySignalValueType.None);
            builder.AddOutput("started", EntitySignalValueType.None);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddOutput("cancelled", EntitySignalValueType.None);
            builder.AddProperty("busy", EntitySignalValueType.Bool, readable: true, writable: false);
            builder.AddProperty("progress", EntitySignalValueType.Float, readable: true, writable: false);
            builder.AddProperty("profile_id", EntitySignalValueType.String, readable: true, writable: false);
        }
    }
}
