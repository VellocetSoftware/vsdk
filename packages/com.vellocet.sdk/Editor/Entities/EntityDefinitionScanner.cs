// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Editor.Entities
{
    internal static class EntityDefinitionScanner
    {
        private static readonly Dictionary<Type, string[]> SignalNamesByEnumType = new();

        internal static List<MarkerInfo> ScanMarkers()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(assembly => assembly != null && !assembly.IsDynamic)
                .SelectMany(GetTypesSafely)
                .Where(type => type != null && typeof(EntityMarker).IsAssignableFrom(type) && !type.IsAbstract &&
                               !type.IsGenericTypeDefinition)
                .Select(CreateMarkerInfo)
                .Where(info => info != null)
                .OrderBy(info => info.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(info => info.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(info => info.EntityClassName, StringComparer.Ordinal)
                .ToList();
        }

        private static MarkerInfo CreateMarkerInfo(Type markerType)
        {
            var entityClassName = EntityMarker.GetEntityClassName(markerType);
            var authoring = markerType.GetCustomAttribute<EntityAuthoringAttribute>(false);
            var displayName = !string.IsNullOrWhiteSpace(authoring?.DisplayName)
                ? authoring.DisplayName
                : BuildDisplayName(entityClassName, markerType);

            return new MarkerInfo
            {
                MarkerType = markerType,
                EntityClassName = entityClassName,
                DisplayName = displayName,
                Category = string.IsNullOrWhiteSpace(authoring?.Category) ? "General" : authoring.Category,
                PlacementKind = authoring?.PlacementKind ?? GuessPlacementKind(markerType),
                IconPath = EntityMarker.GetIconPath(markerType),
                Inputs = GetInputNames(markerType),
                Outputs = GetOutputNames(markerType)
            };
        }

        private static string BuildDisplayName(string entityClassName, Type markerType)
        {
            if (!string.IsNullOrWhiteSpace(entityClassName))
                return ObjectNames.NicifyVariableName(entityClassName.Replace('.', ' ').Replace('_', ' '));

            var markerName = markerType?.Name ?? string.Empty;
            if (markerName.EndsWith("Marker", StringComparison.Ordinal))
                markerName = markerName[..^"Marker".Length];
            return string.IsNullOrWhiteSpace(markerName) ? "<unknown>" : ObjectNames.NicifyVariableName(markerName);
        }

        private static EntityPlacementKind GuessPlacementKind(Type markerType)
        {
            if (markerType == null)
                return EntityPlacementKind.Point;

            if (markerType.Name.StartsWith("Auto", StringComparison.OrdinalIgnoreCase) ||
                markerType.Name.StartsWith("Timer", StringComparison.OrdinalIgnoreCase))
                return EntityPlacementKind.Logic;

            return markerType.GetCustomAttributes(typeof(RequireComponent), true)
                .OfType<RequireComponent>()
                .SelectMany(attribute => new[] { attribute.m_Type0, attribute.m_Type1, attribute.m_Type2 })
                .Any(requiredType => requiredType != null && typeof(Collider).IsAssignableFrom(requiredType))
                ? EntityPlacementKind.Volume
                : EntityPlacementKind.Point;
        }

        private static string[] GetInputNames(Type markerType)
        {
            return GetSignalNames(markerType, 0);
        }

        private static string[] GetOutputNames(Type markerType)
        {
            return GetSignalNames(markerType, 1);
        }

        private static string[] GetSignalNames(Type markerType, int genericArgumentIndex)
        {
            if (!TryGetEntityMarkerGenericArguments(markerType, out var genericArguments) ||
                genericArgumentIndex < 0 ||
                genericArgumentIndex >= genericArguments.Length)
                return Array.Empty<string>();

            return GetSignalNamesForEnum(genericArguments[genericArgumentIndex]);
        }

        private static bool TryGetEntityMarkerGenericArguments(Type markerType, out Type[] genericArguments)
        {
            for (var current = markerType; current != null && current != typeof(object); current = current.BaseType)
            {
                if (!current.IsGenericType)
                    continue;

                var genericDefinition = current.GetGenericTypeDefinition();
                if (genericDefinition != typeof(EntityMarker<,>))
                    continue;

                genericArguments = current.GetGenericArguments();
                return true;
            }

            genericArguments = null;
            return false;
        }

        private static string[] GetSignalNamesForEnum(Type enumType)
        {
            if (enumType == null || !enumType.IsEnum)
                return Array.Empty<string>();

            if (SignalNamesByEnumType.TryGetValue(enumType, out var cached))
                return cached;

            try
            {
                var signalNamesType = typeof(SignalNames<>).MakeGenericType(enumType);
                var field = signalNamesType.GetField("Array", BindingFlags.Public | BindingFlags.Static);
                var values = field?.GetValue(null) as string[];
                cached = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>();
            }
            catch
            {
                cached = Array.Empty<string>();
            }

            SignalNamesByEnumType[enumType] = cached;
            return cached;
        }

        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        internal sealed class MarkerInfo
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