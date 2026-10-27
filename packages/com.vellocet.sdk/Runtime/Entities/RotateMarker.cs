// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.States;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_rotate")]
    [EntityAuthoring("Movement", EntityPlacementKind.Volume, "Rotate")]
    public sealed class RotateMarker : MovingBrushEntityMarker<RotateMarker.Input, RotateMarker.Output>
    {
        [Flags]
        public enum AxisMask
        {
            None = 0,
            X = 1 << 0,
            Y = 1 << 1,
            Z = 1 << 2,
            All = X | Y | Z
        }

        public enum Input
        {
            Open,
            Close,
            Toggle,
            Lock,
            Unlock,
            SetSpeed,
            SetAngle,
            Start,
            Stop,
            Reverse,
            SetPhase
        }

        public enum Output
        {
            OnOpen,
            OnClose,
            OnStarted,
            OnStopped
        }

        [field: Header("Rotation")]
        [field: Tooltip("Local rotation axes. Select one or more axes.")]
        [field: SerializeField]
        public AxisMask RotationAxes { get; private set; } = AxisMask.Y;

        [field: Tooltip("Door mode only. Signed angle in degrees between closed and open.")]
        [field: SerializeField]
        public float Angle { get; private set; } = 90f;

        [field: Tooltip("Rotation speed in degrees per second.")]
        [field: SerializeField]
        [field: Min(0.01f)]
        public float Speed { get; private set; } = 90f;

        [field: Header("Behavior")]
        [field: Tooltip(
            "Rotate continuously on the server tick instead of moving between door-style endpoints. Open/Start runs the rotator; Close/Stop pauses it.")]
        [field: SerializeField]
        public bool LoopContinuously { get; private set; }

        [field: Tooltip("Continuous mode only. Initial rotation phase in degrees around every selected axis.")]
        [field: SerializeField]
        public float StartPhase { get; private set; }

        [field: Tooltip("Continuous mode only. Start rotating as soon as the entity initializes.")]
        [field: SerializeField]
        public bool StartActive { get; private set; } = true;

        [field: Tooltip("Continuous mode only. Begin rotating in the negative direction.")]
        [field: SerializeField]
        public bool ReverseDirection { get; private set; }

        [field: Tooltip("Door mode only. Automatically close after this delay. -1 never closes automatically.")]
        [field: SerializeField]
        [field: Min(-1f)]
        public float ResetDelay { get; private set; } = -1f;

        [field: Header("Start State")]
        [field: SerializeField]
        public bool StartLocked { get; private set; }

        [field: Tooltip("Door mode only. Begin at the open endpoint.")]
        [field: SerializeField]
        public bool StartOpen { get; private set; }

        public Vector3 LocalAxes => new(
            (RotationAxes & AxisMask.X) != 0 ? 1f : 0f,
            (RotationAxes & AxisMask.Y) != 0 ? 1f : 0f,
            (RotationAxes & AxisMask.Z) != 0 ? 1f : 0f);

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("set_speed", EntitySignalValueType.Float);
            builder.AddInput("set_angle", EntitySignalValueType.Float);
            builder.AddInput("set_phase", EntitySignalValueType.Float);
            builder.AddProperty("is_open", EntitySignalValueType.Bool, true, true);
            builder.AddProperty("is_locked", EntitySignalValueType.Bool, true, true);
            builder.AddProperty("is_active", EntitySignalValueType.Bool, true, true);
            builder.AddProperty("is_reversed", EntitySignalValueType.Bool, true, true);
            builder.AddProperty("speed", EntitySignalValueType.Float, true, true);
            builder.AddProperty("angle", EntitySignalValueType.Float, true, true);
            builder.AddProperty("phase", EntitySignalValueType.Float, true, true);
            builder.AddState("state", nameof(RotateEntityState));
        }
    }
}