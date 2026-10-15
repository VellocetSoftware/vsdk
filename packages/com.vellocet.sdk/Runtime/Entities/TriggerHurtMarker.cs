// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    /// <summary>
    ///     Marker for a trigger volume that periodically damages or heals touching entities.
    /// </summary>
    [EntityClass("trigger_hurt")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Volume, "Trigger Hurt")]
    [DisallowMultipleComponent]
    public sealed partial class TriggerHurtMarker :
        TriggerVolumeMarkerBase<TriggerHurtMarker.Input, TriggerHurtMarker.Output>
    {
        public enum DamageModel
        {
            Normal = 0,
            DoublingWithForgiveness = 1
        }

        public enum Input
        {
            Enable,
            Disable,
            Toggle,
            SetDamage,
            TouchTest,
            Reset
        }

        public enum Output
        {
            OnStartTouch,
            OnStartTouchAll,
            OnEndTouch,
            OnEndTouchAll,
            OnTouching,
            OnNotTouching,
            OnHurt,
            OnHurtPlayer,
            OnEnabled,
            OnDisabled
        }

        [field: Tooltip("Optional tag filter. If set, only colliders with this tag are processed.")]
        [field: SerializeField]
        public string RequiredTag { get; private set; } = string.Empty;


        [field: Tooltip(
            "Which activator categories can be affected. Participants are pawns controlled by humans or bots.")]
        [field: SerializeField]
        public TriggerActivatorMask AllowedActivators { get; private set; } = TriggerActivatorMask.Everything;

        [field: Header("Layer Hint")]
        [field: Tooltip(
            "Physics broad-phase hint only. Gameplay correctness still uses semantic activator filters. Use SDK Layer Set for deterministic cross-project hints like Item.")]
        [field: SerializeField]
        public TriggerLayerHintMode LayerHintMode { get; private set; } = TriggerLayerHintMode.AutoFromActivators;

        [field: Tooltip(
            "Canonical SDK layer set used when Layer Hint Mode is SDK Layer Set.")]
        [field: SerializeField]
        public SdkLayerSet LayerHintSet { get; private set; } = SdkLayerSet.Participants;

        [field: Header("Behavior")]
        [field: Tooltip("Start disabled (won't process touching entities) until enabled by input.")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        [field: Tooltip("Damage dealt each tick. Negative values heal instead of damaging.")]
        [field: SerializeField]
        public float Damage { get; private set; } = 10f;

        [field: Tooltip("Seconds between damage/heal ticks while touching.")]
        [field: SerializeField]
        [field: Min(0.01f)]
        public float DamageIntervalSeconds { get; private set; } = 0.5f;

        [field: Tooltip("How per-target damage changes over time while continuously touching.")]
        [field: SerializeField]
        public DamageModel Model { get; private set; }

        [field: Tooltip(
            "Maximum damage per second for DoublingWithForgiveness. 0 means uncapped.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float DamageCapPerSecond { get; private set; }

        public override void ConfigureSignalContract(Vellocet.Sdk.Entities.Signals.EntitySignalContractBuilder builder)
        {
            builder.AddInput("set_damage", Vellocet.Sdk.Entities.Signals.EntitySignalValueType.Float);
        }

        protected override Color GizmoFillColor => new(0.92f, 0.18f, 0.18f, 0.14f);
        protected override Color GizmoWireColor => new(0.92f, 0.18f, 0.18f, 0.9f);
    }
}