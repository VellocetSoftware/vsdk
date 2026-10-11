// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;

namespace Vellocet.Sdk.Tooling
{
    public interface IToolVolumeVisualProvider
    {
        bool TryGetToolVolumeVisual(out ToolVolumeVisualData data);
    }

    public readonly struct ToolVolumeVisualData
    {
        public ToolVolumeVisualData(Matrix4x4 localToWorldMatrix, Vector3 center, Vector3 size, Color wireColor)
            : this(localToWorldMatrix, center, size, default, wireColor, false)
        {
        }

        public ToolVolumeVisualData(Matrix4x4 localToWorldMatrix, Vector3 center, Vector3 size, Color fillColor,
            Color wireColor)
            : this(localToWorldMatrix, center, size, fillColor, wireColor, true)
        {
        }

        private ToolVolumeVisualData(Matrix4x4 localToWorldMatrix, Vector3 center, Vector3 size, Color fillColor,
            Color wireColor, bool hasFill)
        {
            LocalToWorldMatrix = localToWorldMatrix;
            Center = center;
            Size = size;
            FillColor = fillColor;
            WireColor = wireColor;
            HasFill = hasFill;
        }

        public Matrix4x4 LocalToWorldMatrix { get; }
        public Vector3 Center { get; }
        public Vector3 Size { get; }
        public Color FillColor { get; }
        public Color WireColor { get; }
        public bool HasFill { get; }
    }
}