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
    public enum ObserverPointUsage
    {
        None = 0,
        Spectate = 1 << 0,
        Welcome = 1 << 1,
        MatchSummary = 1 << 2,
        Postgame = 1 << 3
    }

    public enum ObserverPointCameraMode
    {
        FixedPose = 0,
        OrbitAnchor = 1
    }

    public enum ObserverPointCameraMotion
    {
        Static = 0,
        CinematicDrift = 1
    }

    [EntityClass("info_observer_point")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Point, "Observer Point")]
    public sealed class ObserverPointMarker : EntityMarker<ObserverPointMarker.Input, ObserverPointMarker.Output>
    {
#if UNITY_EDITOR
        public override bool ShowAuthoringProperty(string propertyName) =>
            propertyName != "postgameStage" || (Usage & ObserverPointUsage.Postgame) != 0;
        public override void ApplyAuthoringChanges() => ApplyPostgameStagePoseFromEditor(true);
#endif

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

        private const float PoseEpsilon = 0.0001f;
        private const float CinematicDriftYawAmplitude = 3.5f;
        private const float CinematicDriftFrequency = 0.055f;
        private const float CinematicDriftVerticalAmplitude = 0.08f;

        [field: Header("Access")]
        [field: Tooltip("Restricts this observer point to specific teams. Combine flags for multi-team points.")]
        [field: SerializeField]
        public TeamMask Teams { get; private set; } = TeamMask.All;

        [field: Tooltip("Start disabled until enabled by input.")]
        [field: SerializeField]
        public bool StartDisabled { get; private set; }

        [field: Header("Usage")]
        [field: Tooltip("Choose when this camera can be used. Select multiple values if needed.")]
        [field: SerializeField]
        public ObserverPointUsage Usage { get; private set; } = (ObserverPointUsage)~0;

        [field: Header("Ownership Gate")]
        [field: Tooltip(
            "Entity targetname, such as a control point. Only its owning team can use this camera. Leave blank for no ownership check.")]
        [field: SerializeField]
        public string AssociatedTeamEntity { get; private set; } = string.Empty;

        [field: Header("Camera")]
        [field: Tooltip(
            "Fixed Pose uses this position and rotation. Orbit Anchor uses this position as the orbit center and look-at target.")]
        [field: SerializeField]
        public ObserverPointCameraMode CameraMode { get; private set; } = ObserverPointCameraMode.FixedPose;

        [field: Tooltip(
            "Static keeps the exact pose. Cinematic Drift adds gentle camera motion while leaving this marker fixed.")]
        [field: SerializeField]
        public ObserverPointCameraMotion CameraMotion { get; private set; } = ObserverPointCameraMotion.Static;

        [field: Tooltip("Optional camera FOV override in degrees. 0 uses the default camera FOV.")]
        [field: Min(0f)]
        [field: SerializeField]
        public float Fov { get; private set; }

        [Header("Postgame Stage")]
        [Tooltip(
            "Align this camera to the stage using its default viewing pose.")]
        [SerializeField]
        private StagePointMarker postgameStage;

        public StagePointMarker PostgameStage => postgameStage;
        public float CinematicYawAmplitude => CinematicDriftYawAmplitude;
        public float CinematicFrequency => CinematicDriftFrequency;
        public float CinematicVerticalAmplitude => CinematicDriftVerticalAmplitude;

#if UNITY_EDITOR
        internal bool HasPostgameStageAuthoring => postgameStage && HasUsage(Usage, ObserverPointUsage.Postgame);

        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyPostgameStagePoseFromEditor(false);
        }

        public bool ApplyPostgameStagePoseFromEditor(bool recordUndo)
        {
            if (!postgameStage || !HasUsage(Usage, ObserverPointUsage.Postgame))
                return false;

            var stageTransform = postgameStage.transform;
            if (!stageTransform)
                return false;

            var cameraPosition = stageTransform.TransformPoint(StagePointMarker.DefaultViewerLocalPosition);
            var lookTarget =
                stageTransform.TransformPoint(Vector3.up * StagePointMarker.DefaultViewerFocusHeightMeters);
            var lookDirection = lookTarget - cameraPosition;
            if (lookDirection.sqrMagnitude <= PoseEpsilon)
                return false;

            var cameraRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            var poseChanged =
                (transform.position - cameraPosition).sqrMagnitude > PoseEpsilon ||
                Quaternion.Angle(transform.rotation, cameraRotation) > 0.01f;
            var modeChanged = CameraMode != ObserverPointCameraMode.FixedPose;
            var motionChanged = CameraMotion != ObserverPointCameraMotion.CinematicDrift;
            var fovChanged = Mathf.Abs(Fov - StagePointMarker.DefaultViewerFov) > PoseEpsilon;
            if (!poseChanged && !modeChanged && !motionChanged && !fovChanged)
                return false;

            if (recordUndo)
            {
                Undo.RecordObject(transform, "Align Observer Point to Postgame Stage");
                Undo.RecordObject(this, "Align Observer Point to Postgame Stage");
            }

            transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            CameraMode = ObserverPointCameraMode.FixedPose;
            CameraMotion = ObserverPointCameraMotion.CinematicDrift;
            Fov = StagePointMarker.DefaultViewerFov;
            EditorUtility.SetDirty(transform);
            EditorUtility.SetDirty(this);
            return true;
        }

        private void OnDrawGizmos()
        {
            DrawPostgameStageLink();

            if (CameraMode == ObserverPointCameraMode.OrbitAnchor)
                return;

            var t = transform;
            var pos = t.position;
            var rot = Quaternion.LookRotation(t.forward, t.up);
            var size = HandleUtility.GetHandleSize(pos) * 0.65f;

            using (new Handles.DrawingScope(new Color(0.35f, 0.85f, 1f)))
            {
                Handles.ArrowHandleCap(0, pos, rot, size, EventType.Repaint);
            }
        }

        private void DrawPostgameStageLink()
        {
            if (!HasPostgameStageAuthoring)
                return;

            var stageTransform = postgameStage.transform;
            if (!stageTransform)
                return;

            using (new Handles.DrawingScope(new Color(1f, 0.75f, 0.2f, 0.72f)))
            {
                Handles.DrawDottedLine(transform.position, stageTransform.position, 4f);
                Handles.DrawWireDisc(stageTransform.position, Vector3.up,
                    HandleUtility.GetHandleSize(stageTransform.position) * 0.22f);
            }
        }

        private static bool HasUsage(ObserverPointUsage configured, ObserverPointUsage usage)
        {
            return usage != ObserverPointUsage.None && (configured & usage) != 0;
        }
#endif
    }
}