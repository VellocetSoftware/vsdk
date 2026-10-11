// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities;

namespace Vellocet.Sdk.Editor.Entities
{
    internal static class AmbientMarkerGizmos
    {
        private static readonly Color AudibleDistanceColor = new(0.2f, 0.8f, 1f, 0.85f);

        [DrawGizmo(GizmoType.Selected)]
        private static void Draw(AmbientMarker marker, GizmoType gizmoType)
        {
            if (!marker || !marker.isActiveAndEnabled || !marker.gameObject.activeInHierarchy)
                return;

            var radius = float.IsFinite(marker.AudibleDistance)
                ? Mathf.Max(0f, marker.AudibleDistance)
                : 0f;
            if (radius <= 0f)
                return;

            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            Gizmos.color = AudibleDistanceColor;
            Gizmos.matrix = Matrix4x4.identity;

            var emitterPosition = marker.transform.position;
            var volumetricState = string.Empty;
            var drawAudibleRadius = true;
            if (marker.IsVolumetric)
            {
                var sceneView = SceneView.lastActiveSceneView;
                var previewListener = sceneView && sceneView.camera
                    ? sceneView.camera.transform.position
                    : marker.GetVolumetricCenter();
                var listenerInside = marker.ContainsVolumetricPoint(previewListener);
                if (listenerInside)
                {
                    emitterPosition = previewListener;
                    volumetricState = " · 2D non-spatial · runtime emitter stationary";
                    drawAudibleRadius = false;
                }
                else
                {
                    emitterPosition = marker.ResolveVolumetricBoundaryPosition(previewListener);
                    volumetricState = " · closest boundary";
                }
            }

            if (drawAudibleRadius)
                Gizmos.DrawWireSphere(emitterPosition, radius);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;

            var labelOffset = drawAudibleRadius ? radius : 0.5f;
            var labelPosition = emitterPosition + marker.transform.up * labelOffset;
            Handles.Label(labelPosition, $"Wwise outer radius  {radius:0.##} m{volumetricState}");
        }
    }
}
#endif