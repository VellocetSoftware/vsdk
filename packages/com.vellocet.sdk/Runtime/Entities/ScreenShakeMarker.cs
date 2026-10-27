// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("env_screenshake")]
    [EntityAuthoring("Environment", EntityPlacementKind.Point, "Screen Shake")]
    public sealed class ScreenShakeMarker : EntityMarker<ScreenShakeMarker.Input, ScreenShakeMarker.Output>
    {
        public enum Input
        {
            Start,
            Stop,
            SetAmplitude,
            SetDuration,
            SetFrequency
        }

        public enum Output
        {
            OnStart,
            OnStop
        }

        [field: Tooltip("Maximum view-angle displacement in degrees.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float Amplitude { get; private set; } = 1.5f;

        [field: Tooltip("Shake oscillations per second.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float Frequency { get; private set; } = 18f;

        [field: SerializeField]
        [field: Min(0f)]
        public float Duration { get; private set; } = 0.6f;

        [field: Tooltip("World radius. Zero shakes every player equally.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float Radius { get; private set; } = 20f;

        [field: Tooltip("Exponent applied to distance attenuation.")]
        [field: SerializeField]
        [field: Min(0.01f)]
        public float FalloffExponent { get; private set; } = 1f;

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddState("state", nameof(Vellocet.Sdk.Entities.States.EffectEntityState));
            builder.AddInput("set_amplitude", EntitySignalValueType.Float);
            builder.AddInput("set_duration", EntitySignalValueType.Float);
            builder.AddInput("set_frequency", EntitySignalValueType.Float);
            builder.AddProperty("amplitude", EntitySignalValueType.Float, true, true);
            builder.AddProperty("duration", EntitySignalValueType.Float, true, true);
            builder.AddProperty("frequency", EntitySignalValueType.Float, true, true);
        }
    }
}