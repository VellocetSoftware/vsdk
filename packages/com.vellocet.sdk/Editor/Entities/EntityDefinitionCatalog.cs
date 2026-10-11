// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Editor.Entities
{
    public static class EntityDefinitionCatalog
    {
        private static List<Definition> _definitions;
        private static Dictionary<Type, Definition> _definitionsByMarkerType;

        public static IReadOnlyList<Definition> GetDefinitions(bool forceRefresh = false)
        {
            if (forceRefresh || _definitions == null)
                ReloadDefinitions();

            return _definitions;
        }

        public static bool TryGetDefinition(Type markerType, out Definition definition)
        {
            if (_definitionsByMarkerType == null)
                ReloadDefinitions();

            if (markerType != null && _definitionsByMarkerType.TryGetValue(markerType, out definition))
                return true;

            definition = null;
            return false;
        }

        private static void ReloadDefinitions()
        {
            var definitions = new List<Definition>();
            var definitionsByMarkerType = new Dictionary<Type, Definition>();

            foreach (var marker in EntityDefinitionScanner.ScanMarkers())
            {
                if (marker?.MarkerType == null || !typeof(EntityMarker).IsAssignableFrom(marker.MarkerType))
                    continue;

                var definition = new Definition
                {
                    MarkerType = marker.MarkerType,
                    EntityClassName = marker.EntityClassName,
                    DisplayName = marker.DisplayName,
                    Category = marker.Category,
                    PlacementKind = marker.PlacementKind,
                    IconPath = marker.IconPath,
                    Inputs = marker.Inputs ?? Array.Empty<string>(),
                    Outputs = marker.Outputs ?? Array.Empty<string>()
                };

                definitions.Add(definition);
                definitionsByMarkerType[marker.MarkerType] = definition;
            }

            _definitions = definitions
                .OrderBy(definition => definition.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(definition => definition.EntityClassName, StringComparer.Ordinal)
                .ToList();
            _definitionsByMarkerType = definitionsByMarkerType;
        }

        public sealed class Definition
        {
            public string Category;
            public string DisplayName;
            public string EntityClassName;
            public string IconPath;
            public string[] Inputs;
            public Type MarkerType;
            public string[] Outputs;
            public EntityPlacementKind PlacementKind;
        }
    }
}
#endif