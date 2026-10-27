using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_rat_racer")]
    [EntityAuthoring("Recreation / Racing", EntityPlacementKind.Point, "Rat Racing")]
    public sealed class RatRacerMarker : ContactOperationMarker
    {
        [SerializeField] private RatTrackMarker[] raceTracks = Array.Empty<RatTrackMarker>();
        [SerializeField] private ContainerMarker holdingContainer;
        [SerializeField] private bool automaticCountdown = true;
        public RatTrackMarker[] RaceTracks => raceTracks;
        public ContainerMarker HoldingContainer => holdingContainer;
        public bool AutomaticCountdown => automaticCountdown;

        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.rat-racer";
        public override string[] Inputs => new[] { "StartRace", "CancelRace" };
        public override string[] Outputs => new[] { "OnRaceStarted", "OnRaceFinished", "OnBettingOpened", "OnBettingClosed" };
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddProperty("minimum_entrants", EntitySignalValueType.Int, true, false);
            builder.AddProperty("countdown_seconds", EntitySignalValueType.Int, true, false);
            builder.AddInput("select_track", EntitySignalValueType.Int);
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddInput("enter_ui", EntitySignalValueType.None);
            builder.AddOutput("track_changed", EntitySignalValueType.Int);
            builder.AddOutput("rat_accepted", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("track_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("track_index", EntitySignalValueType.Int, true, false);
            builder.AddProperty("entrant_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("is_counting_down", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_betting_open", EntitySignalValueType.Bool, true, false);
            foreach (var input in Inputs) builder.AddInput(input, EntitySignalValueType.None);
            foreach (var output in Outputs) builder.AddOutput(output, EntitySignalValueType.None);
            builder.AddProperty("is_running", EntitySignalValueType.Bool, true, false);
        }
    }
}
