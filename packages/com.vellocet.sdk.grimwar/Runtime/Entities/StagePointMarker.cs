// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities
{
    [Flags]
    public enum StagePointPurpose
    {
        None = 0,
        Postgame = 1 << 0
    }

    [EntityClass("info_stage_point")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Point, "Stage Point")]
    public sealed class StagePointMarker : EntityMarker<StagePointMarker.Input, StagePointMarker.Output>
    {
        public enum Input
        {
            Enable,
            Disable,
            Toggle,
            SetTeam
        }

        public enum Output
        {
            OnEnabled,
            OnDisabled
        }

        public const float DefaultViewerFocusHeightMeters = 1.05f;
        public const float DefaultViewerFov = 35f;
        public static readonly Vector3 DefaultViewerLocalPosition = new(0f, 0.85f, -3.75f);
        public static readonly Vector3 DefaultRosterModelLocalEulerAngles = new(0f, 180f, 0f);

        [field: Header("Stage")]
        [field: Tooltip("Purpose flags. Postgame is used for the winning-team MVP stage.")]
        [field: SerializeField]
        public StagePointPurpose Purposes { get; private set; } = (StagePointPurpose)~0;

        [field: Tooltip("Restricts this stage point to winning teams. Combine flags for multi-team points.")]
        [field: SerializeField]
        public TeamMask Teams { get; private set; } = TeamMask.Order | TeamMask.Union;

        [field: Tooltip("If true, this stage point starts disabled until enabled by input.")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var t = transform;
            var pos = t.position;
            var rot = Quaternion.LookRotation(-t.forward, t.up);
            var size = HandleUtility.GetHandleSize(pos) * 0.65f;

            using (new Handles.DrawingScope(new Color(1f, 0.75f, 0.2f)))
            {
                Handles.ArrowHandleCap(0, pos, rot, size, EventType.Repaint);
                Handles.DrawWireDisc(pos, Vector3.up, size * 0.35f);
            }
        }
#endif
    }
}