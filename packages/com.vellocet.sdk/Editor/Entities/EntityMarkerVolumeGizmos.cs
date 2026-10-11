// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Tooling;

namespace Vellocet.Sdk.Editor.Entities
{
    /// <summary>
    ///     Draws shared scene volume gizmos for markers that expose tool volume visuals.
    /// </summary>
    internal static class EntityMarkerVolumeGizmos
    {
        [DrawGizmo(GizmoType.Selected)]
        private static void Draw(EntityMarker marker, GizmoType gizmoType)
        {
            if (!marker || marker is not IToolVolumeVisualProvider provider)
                return;

            if (!provider.TryGetToolVolumeVisual(out var data))
                return;

            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = data.LocalToWorldMatrix;

            if (data.HasFill)
            {
                Gizmos.color = data.FillColor;
                Gizmos.DrawCube(data.Center, data.Size);
            }

            Gizmos.color = data.WireColor;
            Gizmos.DrawWireCube(data.Center, data.Size);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
#endif