// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Editor.Entities
{
    /// <summary>
    ///     Draws a scene icon for every EntityMarker (and subclasses) using the SDK entity icons and optional Scene-view avatars.
    /// </summary>
    internal static class EntityMarkerGizmos
    {
        // Draw in all states, and make it pickable in Scene view.
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void Draw(EntityMarker marker, GizmoType gizmoType)
        {
            if (!marker) return;
            EntityAvatarPreview.Draw(marker, gizmoType);
            var selected = (gizmoType & GizmoType.Selected) != 0;
            if (selected)
                DrawSelectedMarkerFrame(marker);

            var iconPath = string.IsNullOrWhiteSpace(marker.IconPath) ? Constants.LogoPath : marker.IconPath;
            Gizmos.DrawIcon(marker.transform.position, iconPath, true);
        }

        private static void DrawSelectedMarkerFrame(EntityMarker marker)
        {
            var position = marker.transform.position;
            var handleSize = HandleUtility.GetHandleSize(position);
            var radius = handleSize * 0.34f;

            using (new Handles.DrawingScope(SdkEditorStyle.SceneMarkerHaloSoft))
            {
                Handles.DrawSolidDisc(position, Vector3.up, radius);
            }

            using (new Handles.DrawingScope(SdkEditorStyle.SceneMarkerHalo))
            {
                Handles.DrawWireDisc(position, Vector3.up, radius);
                var sceneView = SceneView.currentDrawingSceneView;
                var sceneCamera = sceneView ? sceneView.camera : null;
                var cameraForward = sceneCamera ? sceneCamera.transform.forward : Vector3.forward;
                Handles.DrawWireDisc(position, cameraForward, radius * 0.72f);
            }
        }
    }
}
#endif