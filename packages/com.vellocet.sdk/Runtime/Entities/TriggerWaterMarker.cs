// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    /// <summary>
    ///     Authors a swimmable water volume whose upper face is rendered with the selected material.
    /// </summary>
    [EntityClass("trigger_water")]
    [EntityAuthoring("Environment", EntityPlacementKind.Volume, "Water")]
    [DisallowMultipleComponent]
    public sealed class TriggerWaterMarker :
        TriggerVolumeMarkerBase<TriggerWaterMarker.Input, TriggerWaterMarker.Output>
    {
        public enum Input
        {
        }

        public enum Output
        {
        }

        [field: Header("Surface")]
        [field: Tooltip("Optional material rendered across the water volume's upper face. " +
                        "When empty, the game uses its default water material.")]
        [field: SerializeField]
        public Material SurfaceMaterial { get; private set; }

        protected override SdkLayerSet DefaultLayerSet =>
            SdkLayerSet.Pawn | SdkLayerSet.Ragdoll | SdkLayerSet.Item;
        protected override Color GizmoFillColor => new(0.08f, 0.55f, 0.82f, 0.16f);
        protected override Color GizmoWireColor => new(0.18f, 0.78f, 1f, 0.95f);
    }
}
