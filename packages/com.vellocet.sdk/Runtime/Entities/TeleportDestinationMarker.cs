// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("info_teleport_destination")]
    [EntityAuthoring("Movement", EntityPlacementKind.Point, "Teleport Destination")]
    public sealed class TeleportDestinationMarker :
        DirectionalPointMarker<TeleportDestinationMarker.Input, TeleportDestinationMarker.Output>
    {
        public override Vellocet.Sdk.Entities.Signals.EntitySignalCapabilities SignalCapabilities =>
            Vellocet.Sdk.Entities.Signals.EntitySignalCapabilities.None;

        public enum Input
        {
        }

        public enum Output
        {
        }

        [field: Tooltip("Local-space offset applied to the destination pose.")]
        [field: SerializeField]
        public Vector3 LocalOffset { get; private set; }

        protected override Color DirectionGizmoColor => new(0.67f, 0.28f, 1f);
        protected override Vector3 DirectionGizmoPosition => transform.TransformPoint(LocalOffset);
    }
}