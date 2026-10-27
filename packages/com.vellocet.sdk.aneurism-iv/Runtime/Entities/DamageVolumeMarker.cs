using System;
using Entities.Shared;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_damage")]
    [EntityAuthoring("Triggers", EntityPlacementKind.Volume, "Damage Volume")]
    public sealed class DamageVolumeMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField] private bool destroyItems;
        [SerializeField] private bool instantKill;
        [SerializeField] private bool affectSpirits = true;
        [SerializeField, Tooltip("Health per contact tick: positive damages, negative heals.")] private float damage = 10;
        [SerializeField, Min(.01f)] private float interval = .5f;
        [SerializeField] private TagsWeapon damageType;
        [SerializeField, Tooltip("Apply the first tick on entry instead of after one interval.")] private bool initialDamage;
        [SerializeField] private bool startsDisabled;
        [Tooltip("Stable Fate IDs that are immune to this volume.")]
        [SerializeField] private string[] invulnerableFates = Array.Empty<string>();
        public bool DestroyItems => destroyItems;
        public bool InstantKill => instantKill;
        public bool AffectSpirits => affectSpirits;
        public float Damage => damage;
        public float Interval => interval;
        public TagsWeapon DamageType => damageType;
        public bool InitialDamage => initialDamage;
        public bool StartsDisabled => startsDisabled;
        public string[] InvulnerableFates => invulnerableFates;
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.Operational;
        public override string[] Inputs => new[] { "Enable", "Disable", "Toggle", "SetDamage" };
        public override string[] Outputs => new[] { "OnEnabled", "OnDisabled" };

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("health_changed", EntitySignalValueType.Float);
            builder.AddProperty("participant_count", EntitySignalValueType.Int, true, false);
            builder.AddInput("Enable", EntitySignalValueType.None);
            builder.AddInput("Disable", EntitySignalValueType.None);
            builder.AddInput("Toggle", EntitySignalValueType.None);
            builder.AddInput("SetDamage", EntitySignalValueType.Float);
            builder.AddOutput("OnEnabled", EntitySignalValueType.None);
            builder.AddOutput("OnDisabled", EntitySignalValueType.None);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
        }
    }
}
