// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("env_explosion")]
    [EntityAuthoring("Effects", EntityPlacementKind.Point, "Explosion")]
    public sealed class ExplosionMarker : EntityMarker<ExplosionMarker.Input, ExplosionMarker.Output>
    {
        public enum Input
        {
            Explode,
            Enable,
            Disable,
            SetDamage,
            SetRadius,
            SetForce
        }

        public enum Output
        {
            OnExplode
        }

        [field: Header("Damage")]
        [field: SerializeField]
        [field: Min(0f)]
        public float Damage { get; private set; } = 100f;

        [field: SerializeField]
        [field: Min(0f)]
        public float Radius { get; private set; } = 5f;

        [field: Tooltip("Radial force supplied to damage and physics reactions.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float Force { get; private set; } = 60f;

        [field: Tooltip("Apply Unity explosion force to dynamic rigidbodies in range.")]
        [field: SerializeField]
        public bool AffectRigidbodies { get; private set; } = true;

        [field: Header("Presentation")]
        [field: Tooltip("Optional prefab instantiated on clients when the explosion fires.")]
        [field: SerializeField]
        public GameObject EffectPrefab { get; private set; }

        [field: SerializeField]
        [field: Min(0f)]
        public float EffectLifetime { get; private set; } = 5f;

        [field: Header("Start State")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("set_damage", EntitySignalValueType.Float);
            builder.AddInput("set_radius", EntitySignalValueType.Float);
            builder.AddInput("set_force", EntitySignalValueType.Float);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, true);
            builder.AddProperty("damage", EntitySignalValueType.Float, true, true);
            builder.AddProperty("radius", EntitySignalValueType.Float, true, true);
            builder.AddProperty("force", EntitySignalValueType.Float, true, true);
        }
    }
}