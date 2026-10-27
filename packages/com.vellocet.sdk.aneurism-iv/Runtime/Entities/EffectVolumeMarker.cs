using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_effect")]
    [EntityAuthoring("Triggers", EntityPlacementKind.Volume, "Game Effect Volume")]
    public sealed class EffectVolumeMarker : EntityMarker, IBoundedEntityMarker
    {
        public enum EffectProfile { Fire, BurnBarrel }
        [SerializeField] private EffectProfile profile;
        [SerializeField] private bool startDisabled;
        public EffectProfile Profile => profile;
        public bool StartDisabled => startDisabled;
        public override string[] Inputs => new[] { "enable", "disable", "toggle" };
        public override string[] Outputs => new[] { "enabled", "disabled", "effect_applied" };
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            foreach (var input in Inputs) builder.AddInput(input, EntitySignalValueType.None);
            builder.AddOutput("enabled", EntitySignalValueType.None);
            builder.AddOutput("disabled", EntitySignalValueType.None);
            builder.AddOutput("effect_applied", EntitySignalValueType.None);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
        }
    }
}
