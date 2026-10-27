using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_loading_bay")]
    [EntityAuthoring("Shipping", EntityPlacementKind.Point, "Loading Bay", HiddenFromPlacement = true, Description = "Native loading bay. For custom layouts, place crates, a manifest, supply points and doors independently.")]
    public sealed class LoadingBayMarker : PrefabEntityMarker
    {
        [SerializeField, Min(1)] private int quotaPerCrate = 4;
        [SerializeField, Tooltip("Items requested by each shipping cycle. An empty list leaves the bay idle.")] private string[] merchandise = Array.Empty<string>();
        public int QuotaPerCrate => quotaPerCrate;
        public System.Collections.Generic.IReadOnlyList<string> Merchandise => merchandise;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.loading-bay";
        public override string[] Inputs => new[] { "Ship" };
        public override string[] Outputs => new[] { "OnReady", "OnShipped" };

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("Ship", EntitySignalValueType.None);
            builder.AddOutput("OnReady", EntitySignalValueType.None);
            builder.AddOutput("OnShipped", EntitySignalValueType.None);
            builder.AddProperty("is_ready", EntitySignalValueType.Bool, true, false);
        }
    }
}
