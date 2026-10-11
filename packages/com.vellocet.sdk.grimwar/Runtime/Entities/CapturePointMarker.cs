// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("game_capture_point")]
    [EntityAuthoring("Objectives", EntityPlacementKind.Volume, "Capture Point")]
    public class
        CapturePointMarker : TriggerVolumeMarkerBase<CapturePointMarker.Input, CapturePointMarker.Output>
    {
        public enum Input
        {
            Lock,
            Unlock,
            Reset
        }

        public enum Output
        {
            OnCaptured,
            OnLocked,
            OnUnlocked,
            OnOwnerChanged
        }

        [field: Header("Display")]
        [field: Tooltip("Display name for the capture point.")]
        [field: SerializeField]
        public string PointName { get; private set; } = "A";

        [field: Header("Ownership")]
        [field: Tooltip("Team that starts in control of the point.")]
        [field: SerializeField]
        public Team OwningTeam { get; private set; } = Team.None;

        [field: Header("Capture")]
        [field: Tooltip("Seconds required to capture when uncontested.")]
        [field: SerializeField]
        [field: Min(1f)]
        public float CaptureSeconds { get; protected set; } = 10f;

        [field: Tooltip("Seconds required to decay capture progress back to neutral.")]
        [field: SerializeField]
        [field: Min(1f)]
        public float DecaySeconds { get; protected set; } = 5f;

        [field: Tooltip("Maximum number of pawns contributing to capture speed.")]
        [field: SerializeField]
        [field: Min(1)]
        public int MaxCappers { get; private set; } = 3;

        [field: Tooltip("Seconds added to the round timer when this point is captured in supported modes.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float CaptureTimeBonusSeconds { get; private set; } = 60f;

        [field: Tooltip(
            "Capture points require all listed points. Uplink flags list preceding flags in the Union-to-Order direction: any one unlocks this flag for Union, and Order follows the same graph in reverse.")]
        [field: SerializeField]
        public CapturePointMarker[] Prerequisites { get; private set; } = Array.Empty<CapturePointMarker>();

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddProperty("owning_team", EntitySignalValueType.Enum, true, false, nameof(Team));
        }

        protected override bool DefaultIgnoreFromNavMeshBuild => true;

        public bool HasExplicitPrerequisites => Prerequisites is { Length: > 0 };

        protected override Color GizmoFillColor => new(0.12f, 0.82f, 0.98f, 0.14f);
        protected override Color GizmoWireColor => new(0.12f, 0.82f, 0.98f, 0.9f);

        public IReadOnlyList<CapturePointMarker> GetPrerequisiteMarkers()
        {
            return Prerequisites ?? Array.Empty<CapturePointMarker>();
        }
    }
}