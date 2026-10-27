using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("env_decorative_machine")]
    [EntityAuthoring("Environment / Effects", EntityPlacementKind.Point, "Decorative Machine")]
    public sealed class SubmachineMarker : PrefabEntityMarker
    {
        // Existing maps retain manual cues; the game profile enables the native cycle for new placements.
        [SerializeField, Tooltip("Run the standard charge, flash and cooldown cycle. Disable when a map script owns timing.")]
        private bool automaticCycle;
        public bool AutomaticCycle => automaticCycle;
        public override string ArchetypeId => "aniv.submachine";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("start_sound", EntitySignalValueType.None);
            builder.AddInput("stop_sound", EntitySignalValueType.None);
            builder.AddInput("set_flash", EntitySignalValueType.Float);
            builder.AddOutput("started", EntitySignalValueType.None);
            builder.AddOutput("stopped", EntitySignalValueType.None);
            builder.AddProperty("flash_intensity", EntitySignalValueType.Float, true, false);
        }
    }
}
