using UnityEngine;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("env_warplanes")]
    [EntityAuthoring("Wartime", EntityPlacementKind.Point, "Warplane Flyby",
        Description = "Complete wartime flyby with audio lead-in, planes and automatic repetition. Start, pulse and stop inputs also support authored events.")]
    public sealed class WarplanesMarker : PrefabEntityMarker
    {
        // Missing values on existing scenes and portable maps retain their script-driven cue behavior.
        // The game profile enables this default for newly placed Vertex entities.
        [SerializeField, Tooltip("Automatically repeat flybys during wartime. Disable when a map script owns timing.")]
        private bool followWartime;
        [SerializeField, Min(0.01f)] private float interval = 57.5f;
        [SerializeField, Min(0)] private float leadIn = 12.5f;
        [SerializeField, Min(0.01f)] private float visibleTime = 20f;
        public bool FollowWartime => followWartime;
        public float Interval => interval;
        public float LeadIn => leadIn;
        public float VisibleTime => visibleTime;
        public override string ArchetypeId => "aniv.warplanes";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("start", EntitySignalValueType.None);
            builder.AddInput("pulse", EntitySignalValueType.None);
            builder.AddInput("play_audio", EntitySignalValueType.None);
            builder.AddInput("show_planes", EntitySignalValueType.None);
            builder.AddInput("stop", EntitySignalValueType.None);
            builder.AddOutput("started", EntitySignalValueType.None);
            builder.AddOutput("audio_started", EntitySignalValueType.None);
            builder.AddOutput("planes_shown", EntitySignalValueType.None);
            builder.AddOutput("stopped", EntitySignalValueType.None);
            builder.AddProperty("is_running", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_showing", EntitySignalValueType.Bool, true, false);
        }
    }
}
