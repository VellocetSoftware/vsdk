// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_team_barrier")]
    [EntityAuthoring("World", EntityPlacementKind.Volume, "Team Barrier")]
    public sealed class
        TeamBarrierMarker : TriggerVolumeMarkerBase<TeamBarrierMarker.Input, TeamBarrierMarker.Output>
    {
        public enum Input
        {
            Enable,
            Disable,
            Toggle,
            SetAllowedTeams,
            SetProjectileInteraction,
            SetVisualEnabled
        }

        public enum Output
        {
            OnEnabled,
            OnDisabled
        }

        public enum ProjectileInteractionMode
        {
            PassThrough,
            BlockTeamsDeniedMovement,
            BlockAll
        }

        [field: Header("Teams")]
        [field: Tooltip("Teams that can pass through this barrier. Teams outside this mask are blocked.")]
        [field: SerializeField]
        public TeamMask AllowedTeams { get; private set; } = TeamMask.Union;

        [field: Header("Projectiles")]
        [field:
            Tooltip(
                "How projectiles interact with this barrier. Pass Through lets every team shoot through while movement remains team-gated.")]
        [field: SerializeField]
        public ProjectileInteractionMode ProjectileInteraction { get; private set; } =
            ProjectileInteractionMode.PassThrough;

        [field: Header("Start State")]
        [field: Tooltip("If true, this barrier starts disabled and does not block anyone.")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        [field: Header("Presentation")]
        [field: Tooltip("If true, show the barrier visual while the barrier is enabled.")]
        [field: SerializeField]
        public bool VisualEnabled { get; private set; } = true;

        protected override Color GizmoFillColor => new(0.15f, 0.65f, 1f, 0.18f);
        protected override Color GizmoWireColor => new(0.15f, 0.65f, 1f, 0.9f);
        protected override bool DefaultIgnoreFromNavMeshBuild => true;

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("set_allowed_teams", EntitySignalValueType.Enum);
            builder.AddInput("set_projectile_interaction", EntitySignalValueType.Enum);
            builder.AddInput("set_visual_enabled", EntitySignalValueType.Bool);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, true);
            builder.AddProperty("allowed_teams", EntitySignalValueType.Enum, true, true, nameof(TeamMask));
            builder.AddProperty("projectile_interaction", EntitySignalValueType.Enum, true, true,
                nameof(ProjectileInteractionMode));
            builder.AddProperty("visual_enabled", EntitySignalValueType.Bool, true, true);
        }
    }
}