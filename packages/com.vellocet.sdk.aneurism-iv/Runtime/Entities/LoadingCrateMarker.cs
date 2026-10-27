using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_loading_crate")]
    [EntityAuthoring("Shipping", EntityPlacementKind.Point, "Loading Crate")]
    public sealed class LoadingCrateMarker : PrefabEntityMarker
    {
        [SerializeField] private string merchandise = GameItem.Coal;
        [SerializeField, Range(1, 1000)] private int quota = 4;
        public string Merchandise => merchandise;
        public int Quota => quota;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.loading-crate";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("ship", EntitySignalValueType.None);
            builder.AddInput("reset", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("phase", EntitySignalValueType.String, true, false);
            builder.AddProperty("signer", EntitySignalValueType.String, true, false);
            builder.AddOutput("ready", EntitySignalValueType.None);
            builder.AddOutput("shipped", EntitySignalValueType.None);
            builder.AddOutput("reset", EntitySignalValueType.None);
            builder.AddProperty("is_ready", EntitySignalValueType.Bool, readable: true, writable: false);
            builder.AddProperty("remaining", EntitySignalValueType.Int, readable: true, writable: false);
        }
    }
}
