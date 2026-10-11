// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities;

namespace Vellocet.Sdk.Editor.Entities
{
    /// <summary>
    ///     Discovers ambient events directly from a Wwise project's event work units without requiring WAAPI or a
    ///     running Wwise Authoring process. The SDK export compiler uses this same scanner, keeping live and shipped
    ///     catalogs on one set of inclusion rules.
    /// </summary>
    public static class AmbientEventDiscovery
    {
        private const string WwiseSettingsFileName = "WwiseSettings.xml";
        private const string WwiseProjectPathElement = "WwiseProjectPath";
        private const string EventsDirectoryName = "Events";

        public static bool TryDiscoverCurrentProject(out Snapshot snapshot, out string error)
        {
            return TryDiscover(Application.dataPath, out snapshot, out error);
        }

        public static bool TryDiscover(string assetsPath, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(assetsPath))
                {
                    error = "Unity Assets path is required to discover Wwise ambient events.";
                    return false;
                }

                var normalizedAssetsPath = Path.GetFullPath(assetsPath);
                var settingsPath = Path.Combine(normalizedAssetsPath, WwiseSettingsFileName);
                if (!File.Exists(settingsPath))
                {
                    error = $"Wwise integration settings were not found: {settingsPath}";
                    return false;
                }

                if (!TryReadWwiseProjectPath(settingsPath, normalizedAssetsPath, out var projectPath, out error))
                    return false;

                if (!TryReadProjectIdentity(projectPath, out var projectName, out var projectId, out error))
                    return false;

                var eventRoot = Path.Combine(Path.GetDirectoryName(projectPath) ?? string.Empty,
                    EventsDirectoryName);
                if (!Directory.Exists(eventRoot))
                {
                    error = $"Wwise Events directory was not found: {eventRoot}";
                    return false;
                }

                var eventNames = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var workUnitPath in Directory.EnumerateFiles(eventRoot, "*.wwu", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.Ordinal))
                    ReadAmbientEvents(workUnitPath, eventNames);

                snapshot = new Snapshot(projectPath, projectName, projectId, eventNames.ToArray());
                return true;
            }
            catch (Exception ex)
            {
                error = $"Unable to discover Wwise ambient events: {ex.Message}";
                return false;
            }
        }

        private static bool TryReadWwiseProjectPath(string settingsPath, string assetsPath, out string projectPath,
            out string error)
        {
            projectPath = string.Empty;
            error = string.Empty;

            using var reader = CreateReader(settingsPath);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element ||
                    !string.Equals(reader.LocalName, WwiseProjectPathElement, StringComparison.Ordinal))
                    continue;

                var configuredPath = reader.ReadElementContentAsString()?.Trim();
                if (string.IsNullOrWhiteSpace(configuredPath))
                {
                    error = $"{WwiseProjectPathElement} is empty in {settingsPath}.";
                    return false;
                }

                configuredPath = configuredPath
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
                projectPath = Path.GetFullPath(Path.IsPathRooted(configuredPath)
                    ? configuredPath
                    : Path.Combine(assetsPath, configuredPath));

                if (!File.Exists(projectPath))
                {
                    error = $"Configured Wwise project was not found: {projectPath}";
                    return false;
                }

                return true;
            }

            error = $"{WwiseProjectPathElement} was not found in {settingsPath}.";
            return false;
        }

        private static bool TryReadProjectIdentity(string projectPath, out string projectName, out string projectId,
            out string error)
        {
            projectName = string.Empty;
            projectId = string.Empty;
            error = string.Empty;

            using var reader = CreateReader(projectPath);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element ||
                    !string.Equals(reader.LocalName, "Project", StringComparison.Ordinal))
                    continue;

                projectName = reader.GetAttribute("Name")?.Trim() ?? string.Empty;
                var rawProjectId = reader.GetAttribute("ID")?.Trim();
                if (!Guid.TryParse(rawProjectId, out var parsedProjectId))
                {
                    error = $"Wwise project has an invalid or missing Project ID: {projectPath}";
                    return false;
                }

                projectId = parsedProjectId.ToString("D").ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(projectName))
                {
                    error = $"Wwise project has no Project name: {projectPath}";
                    return false;
                }

                return true;
            }

            error = $"Wwise project metadata was not found: {projectPath}";
            return false;
        }

        private static void ReadAmbientEvents(string workUnitPath, ISet<string> eventNames)
        {
            using var reader = CreateReader(workUnitPath);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element ||
                    !string.Equals(reader.LocalName, "Event", StringComparison.Ordinal))
                    continue;

                var eventName = reader.GetAttribute("Name")?.Trim();
                if (!string.IsNullOrWhiteSpace(eventName) &&
                    eventName.StartsWith(AmbientMarker.EventNamePrefix, StringComparison.Ordinal))
                    eventNames.Add(eventName);
            }
        }

        private static XmlReader CreateReader(string path)
        {
            return XmlReader.Create(path, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                XmlResolver = null
            });
        }

        public sealed class Snapshot
        {
            internal Snapshot(string projectPath, string projectName, string projectId, string[] eventNames)
            {
                ProjectPath = projectPath;
                ProjectName = projectName;
                ProjectId = projectId;
                EventNames = eventNames ?? Array.Empty<string>();
            }

            public string ProjectPath { get; }
            public string ProjectName { get; }
            public string ProjectId { get; }
            public IReadOnlyList<string> EventNames { get; }
        }
    }

    internal static class AmbientEventCatalog
    {
        private static IReadOnlyList<string> _compiledEventNames = Array.Empty<string>();
        private static string _compiledProjectId = string.Empty;
        public static void RegisterCompiled(string projectId, IReadOnlyList<string> names)
        {
            _compiledProjectId = projectId ?? string.Empty;
            _compiledEventNames = names ?? Array.Empty<string>();
            _initialized = false;
        }

        private const double RefreshIntervalSeconds = 2d;

        private static IReadOnlyList<string> _eventNames = Array.Empty<string>();
        private static string _error = string.Empty;
        private static double _refreshAfter;
        private static bool _initialized;

        internal static IReadOnlyList<string> GetEventNames(out string error)
        {
            if (!_initialized || EditorApplication.timeSinceStartup >= _refreshAfter)
                Refresh();

            error = _error;
            return _eventNames;
        }

        internal static void Invalidate()
        {
            _initialized = false;
            _refreshAfter = 0d;
        }

        private static void Refresh()
        {
            _initialized = true;
            _refreshAfter = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;

            if (AmbientEventDiscovery.TryDiscoverCurrentProject(out var live, out var liveError) &&
                IsMatchingProject(live.ProjectId))
            {
                _eventNames = live.EventNames;
                _error = live.EventNames.Count == 0
                    ? $"No Wwise Events beginning with '{AmbientMarker.EventNamePrefix}' were found."
                    : string.Empty;
                return;
            }

            _eventNames = _compiledEventNames ?? Array.Empty<string>();
            _error = _eventNames.Count > 0
                ? string.Empty
                : string.IsNullOrWhiteSpace(liveError)
                    ? $"No compiled ambient Events beginning with '{AmbientMarker.EventNamePrefix}' are available."
                    : liveError;
        }

        private static bool IsMatchingProject(string projectId)
        {
            return string.IsNullOrWhiteSpace(_compiledProjectId) ||
                   string.Equals(_compiledProjectId, projectId,
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif