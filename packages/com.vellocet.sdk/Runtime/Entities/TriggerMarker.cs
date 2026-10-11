// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    /// <summary>
    ///     Marker for a generic multi-fire trigger.
    /// </summary>
    [EntityClass("trigger_multiple")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Volume, "Trigger")]
    [DisallowMultipleComponent]
    public sealed partial class TriggerMarker : TriggerVolumeMarkerBase<TriggerMarker.Input, TriggerMarker.Output>
    {
        public enum Input
        {
            Enable,
            Disable,
            Toggle,
            Fire,
            Reset,
            TouchTest,
            Execute
        }

        public enum Output
        {
            OnStartTouch,
            OnStartTouchAll, // first valid enters when none were touching
            OnEndTouch,
            OnEndTouchAll, // last valid leaves (none touching)
            OnTrigger,
            OnTouching, // for TouchTest
            OnNotTouching, // for TouchTest
            OnEnabled,
            OnDisabled,
            OnExecuted
        }

        [field: Tooltip("Only colliders with this tag activate the trigger. Leave blank to allow any tag.")]
        [field: SerializeField]
        public string RequiredTag { get; private set; } = string.Empty;


        [field: Tooltip(
            "Participants are human or bot pawns. UnassignedPawns have a Pawn component with no participant.")]
        [field: SerializeField]
        public TriggerActivatorMask AllowedActivators { get; private set; } = TriggerActivatorMask.Participants;

        [field: Header("Layer Hint")]
        [field: Tooltip(
            "Filters physics candidates; Allowed Activators still decides who triggers. Use SDK Layer Set for shared layers such as Item.")]
        [field: SerializeField]
        public TriggerLayerHintMode LayerHintMode { get; private set; } = TriggerLayerHintMode.AutoFromActivators;

        [field: Tooltip(
            "Layers used when Layer Hint Mode is SDK Layer Set.")]
        [field: SerializeField]
        public SdkLayerSet LayerHintSet { get; private set; } = SdkLayerSet.Participants;

        [field: Header("Behavior")]
        [field: Tooltip("Ignore touches until enabled by input.")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        [field: Tooltip(
            "Seconds between firings while occupied. Use 0.2 for typical triggers; 0 fires every frame.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float WaitSeconds { get; private set; } = 0.2f;

        [field: Tooltip("Delay (seconds) before the first OnTrigger after an activator enters.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float DelaySeconds { get; private set; }

        [field: Tooltip("Disable after the first OnTrigger.")]
        [field: SerializeField]
        public bool FireOnce { get; private set; }

        [field:
            Tooltip("Seconds between OnTouching events while occupied. 0 disables them.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float TouchingIntervalSeconds { get; private set; }

        protected override Color GizmoFillColor => new(0.98f, 0.72f, 0.12f, 0.14f);
        protected override Color GizmoWireColor => new(0.98f, 0.72f, 0.12f, 0.9f);

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            base.ConfigureSignalContract(builder);
            builder.AddInput("execute", EntitySignalValueType.None);
            builder.AddOutput("executed");
        }
    }
}