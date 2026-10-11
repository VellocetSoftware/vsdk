// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;

namespace Vellocet.Sdk.Spatial
{
    [DisallowMultipleComponent]
    public sealed class SpatialOctreeBakeSettingsSource : MonoBehaviour
    {
        [SerializeField] private SpatialOctreeBakeSettings settings = SpatialOctreeBakeSettings.Default;

        public SpatialOctreeBakeSettings Settings => settings.Sanitize();
    }
}