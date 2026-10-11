// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.States;

namespace Vellocet.Sdk.Entities
{
    /// <summary>
    ///     Marker for a simple breakable (func_breakable-style) entity.
    /// </summary>
    [EntityClass("func_damageable")]
    [EntityAuthoring("World", EntityPlacementKind.Volume, "Damageable")]
    [RequireComponent(typeof(BoxCollider))]
    public sealed partial class
        DamageableMarker : EntityMarker<DamageableMarker.Input, DamageableMarker.Output>
    {
        public enum BreakPresentationMode
        {
            HideAndDisableCollision,
            StateOnly
        }

        public enum Input
        {
            Damage,
            Heal,
            Break,
            Reset
        }

        public enum Output
        {
            OnDamaged,
            OnBreak,
            OnReset,
            OnHealed,
            OnHealthChanged
        }

        [field: Header("Health")]
        [field: Tooltip("Maximum health before the damageable enters its broken state.")]
        [field: SerializeField]
        [field: Min(1f)]
        public float MaxHealth { get; private set; } = 100f;

        [field: Tooltip("If true, the damageable starts operationally disabled and ignores gameplay damage.")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        [field: Tooltip("If true, the breakable starts broken (health = 0).")]
        [field: SerializeField]
        public bool StartsBroken { get; private set; }

        [field: Header("Break Presentation")]
        [field:
            Tooltip(
                "Default behavior hides owned renderers and disables owned colliders while broken. State Only keeps the authored presentation and collision unchanged.")]
        [field: SerializeField]
        public BreakPresentationMode BreakPresentation { get; private set; } =
            BreakPresentationMode.HideAndDisableCollision;

        [field:
            Tooltip(
                "Optional owned subtree to hide and make non-solid when broken. Leave empty to use this marker and all non-entity children.")]
        [field: SerializeField]
        public Transform BreakRoot { get; private set; }

        [field: Header("Damage Filter")]
        [field: Tooltip("Ignore individual damage events below this threshold.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float MinDamage { get; private set; }

        [field: Header("Reset")]
        [field: Tooltip("Seconds before restoring to full health after breaking. -1 = never auto-reset.")]
        [field: SerializeField]
        [field: Min(-1f)]
        public float ResetDelaySeconds { get; private set; } = -1f;


        public override EntitySignalCapabilities SignalCapabilities =>
            base.SignalCapabilities | EntitySignalCapabilities.Operational;

        protected override bool DefaultIgnoreFromNavMeshBuild => true;

        public Transform ResolveBreakRoot()
        {
            return BreakRoot && BreakRoot.IsChildOf(transform) ? BreakRoot : transform;
        }

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("health_changed", EntitySignalValueType.Float);
            builder.AddState("state", nameof(DamageableEntityState));
            builder.AddProperty("health", EntitySignalValueType.Float, true, false);
            builder.AddProperty("max_health", EntitySignalValueType.Float, true, false);
            builder.AddProperty("is_broken", EntitySignalValueType.Bool, true, false);
        }
    }
}
