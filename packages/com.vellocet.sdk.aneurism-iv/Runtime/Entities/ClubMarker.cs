using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_club")]
    [EntityAuthoring("Environment", EntityPlacementKind.Point, "Club")]
    public sealed class ClubMarker : ContactOperationMarker
    {
        [SerializeField] private bool useBuiltInRecovery = true;
        public bool UseBuiltInRecovery => useBuiltInRecovery;

        public override string ArchetypeId => "aniv.club";
        public override string[] Inputs => new[] { "NextTrack" };
        public override string[] Outputs => new[] { "OnTrackChanged" };
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("restore_sanity", EntitySignalValueType.None);
            builder.AddOutput("sanity_restored", EntitySignalValueType.Int);
            builder.AddProperty("can_restore_sanity", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("participant_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("track_index", EntitySignalValueType.Int, true, false);
            builder.AddProperty("track_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("track_time", EntitySignalValueType.Float, true, false);
            builder.AddInput("NextTrack", EntitySignalValueType.None);
            builder.AddOutput("OnTrackChanged", EntitySignalValueType.None);
        }
    }
}
