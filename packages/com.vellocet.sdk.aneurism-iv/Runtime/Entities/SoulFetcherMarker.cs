using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_soul_fetcher")]
    [EntityAuthoring("Environment / Rot", EntityPlacementKind.Point, "Soul Fetcher")]
    public sealed class SoulFetcherMarker : OperationEntityMarker
    {
        public enum AnimationKind { SoulFetcher, Player }
        [SerializeField] private AnimationKind animation;
        public AnimationKind Animation => animation;
        [Tooltip("Stable Fate ID; empty leaves the fetcher unassigned.")]
        [SerializeField] private string fate = "prole";
        public string FateId => fate;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.soul-fetcher";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("use", EntitySignalValueType.None);
            builder.AddOutput("started", EntitySignalValueType.String);
            builder.AddOutput("completed", EntitySignalValueType.String);
            builder.AddOutput("cancelled", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("fate_id", EntitySignalValueType.String, true, false);
            builder.AddProperty("pending_count", EntitySignalValueType.Int, true, false);
        }
    }
}
