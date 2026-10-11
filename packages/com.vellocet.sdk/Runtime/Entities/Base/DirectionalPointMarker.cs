// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities.Base
{
    /// <summary>
    ///     Point entity whose transform forward axis defines an authored world direction.
    /// </summary>
    public abstract class DirectionalPointMarker<TInput, TOutput> : EntityMarker<TInput, TOutput>
        where TInput : struct, Enum
        where TOutput : struct, Enum
    {
        public Vector3 WorldDirection => transform.forward;

        protected virtual Color DirectionGizmoColor => Color.yellow;
        protected virtual Vector3 DirectionGizmoPosition => transform.position;
        protected virtual float DirectionGizmoScale => 0.75f;

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            var markerTransform = transform;
            var position = DirectionGizmoPosition;
            var rotation = Quaternion.LookRotation(WorldDirection, markerTransform.up);
            var size = HandleUtility.GetHandleSize(position) * DirectionGizmoScale;

            using (new Handles.DrawingScope(DirectionGizmoColor))
            {
                Handles.ArrowHandleCap(0, position, rotation, size, EventType.Repaint);
            }
        }
#endif
    }
}