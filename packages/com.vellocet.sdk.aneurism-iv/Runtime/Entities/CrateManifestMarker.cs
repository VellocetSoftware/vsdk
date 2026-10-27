using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("logic_crate_manifest")]
    [EntityAuthoring("Shipping", EntityPlacementKind.Logic, "Crate Manifest")]
    public sealed class CrateManifestMarker : EntityMarker, IMapModelMarker
    {
        [SerializeField, Tooltip("Independent loading crates in this shipment. Their committed phases determine readiness.")]
        private LoadingCrateMarker[] crates = Array.Empty<LoadingCrateMarker>();
        [SerializeField, Tooltip("Publish durable crate cycles at authored destinations instead of aggregating fixed crates.")]
        private bool useSupply;
        [SerializeField] private EntityMarker[] supplyPoints = Array.Empty<EntityMarker>();
        [SerializeField] private string[] merchandise = Array.Empty<string>();
        [SerializeField, Min(1)] private int quotaPerCrate = 4;
        [SerializeField, Min(1)] private int minimumCrates = 1;
        [SerializeField, Min(1)] private int maximumCrates = 1;
        public int MinimumCrates => minimumCrates;
        public int MaximumCrates => maximumCrates;
        public bool UseSupply => useSupply;
        public EntityMarker[] SupplyPoints => supplyPoints;
        public string[] Merchandise => merchandise;
        public int QuotaPerCrate => quotaPerCrate;
        public string ArchetypeId => useSupply ? "aniv.crate-supply" : string.Empty;
        public LoadingCrateMarker[] Crates => crates;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("ship", EntitySignalValueType.None);
            builder.AddInput("reset", EntitySignalValueType.None);
            builder.AddInput("report", EntitySignalValueType.None);
            builder.AddOutput("ready", EntitySignalValueType.None);
            builder.AddOutput("shipped", EntitySignalValueType.None);
            builder.AddOutput("reset", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_ready", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("can_reset", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("crate_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("shipped_count", EntitySignalValueType.Int, true, false);
        }
    }
}
