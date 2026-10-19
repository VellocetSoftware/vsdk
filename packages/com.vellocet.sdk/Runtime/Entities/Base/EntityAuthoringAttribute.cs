// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine.Scripting;

namespace Vellocet.Sdk.Entities.Base
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    [Preserve]
    public sealed class EntityAuthoringAttribute : Attribute
    {
        public EntityAuthoringAttribute(string category, EntityPlacementKind placementKind, string displayName = null)
        {
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
            PlacementKind = placementKind;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        }

        public string Category { get; }
        public string Description { get; set; }
        public bool HiddenFromPlacement { get; set; }
        public string DisplayName { get; }
        public EntityPlacementKind PlacementKind { get; }

        /// <summary>Optional asset:&lt;catalog&gt;/&lt;model&gt; reference used only for the Vertex viewport.</summary>
        public string AvatarModel { get; set; }
    }
}