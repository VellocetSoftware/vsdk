// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities
{
    /// <summary>
    ///     Authors a two-sided ladder volume. Local Y is the climb axis and local Z is depth.
    /// </summary>
    [EntityClass("func_ladder")]
    [EntityAuthoring("Movement", EntityPlacementKind.Volume, "Ladder")]
    [DisallowMultipleComponent]
    public sealed class LadderMarker : TriggerVolumeMarkerBase<LadderMarker.Input, LadderMarker.Output>
    {
        public override Vellocet.Sdk.Entities.Signals.EntitySignalCapabilities SignalCapabilities =>
            Vellocet.Sdk.Entities.Signals.EntitySignalCapabilities.None;

        public enum Input
        {
        }

        public enum Output
        {
        }

        protected override Color GizmoFillColor => new(0.96f, 0.67f, 0.12f, 0.16f);
        protected override Color GizmoWireColor => new(1f, 0.78f, 0.2f, 0.95f);
        protected override Vector3 InitialAuthoringSize => new(1.2f, 3f, 0.25f);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var size = GetBoundsXYZ();
            var center = transform.position + transform.rotation * new Vector3(0f, size.y * 0.5f, 0f);
            var normal = transform.forward;
            var handleSize = HandleUtility.GetHandleSize(center) * 0.55f;

            using (new Handles.DrawingScope(GizmoWireColor))
            {
                Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(normal, transform.up), handleSize,
                    EventType.Repaint);
                Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(-normal, transform.up), handleSize,
                    EventType.Repaint);
            }
        }
#endif
    }
}