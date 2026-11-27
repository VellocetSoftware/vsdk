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
    /// <summary>Reads the built-in ambient cues used by the SDK.</summary>
    public static class AmbientEventDiscovery
    {
        public static bool TryDiscoverCurrentProject(out Snapshot snapshot, out string error) =>
            TryDiscover(Application.dataPath, out snapshot, out error);

        public static bool TryDiscover(string assetsPath, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;
            var path = Path.Combine(assetsPath, "StreamingAssets", "Stereoform", "core", "catalog.json");
            if (!File.Exists(path))
            {
                error = $"The game's audio catalog is missing: {path}";
                return false;
            }

            try
            {
                var catalog = JsonUtility.FromJson<AudioCatalog>(File.ReadAllText(path));
                var names = catalog.events.Select(cue => cue.name)
                    .Where(name => name.StartsWith(AmbientMarker.EventNamePrefix, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.Ordinal).ToArray();
                snapshot = new Snapshot(path, "Grimwar", "grimwar", names);
                return true;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NullReferenceException)
            {
                error = $"Unable to read the audio catalog: {ex.Message}";
                return false;
            }
        }

        [Serializable]
        private sealed class AudioCatalog
        {
            public AudioCue[] events = Array.Empty<AudioCue>();
        }

        [Serializable]
        private sealed class AudioCue
        {
            public string name;
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
                    ? $"No Stereoform Events beginning with '{AmbientMarker.EventNamePrefix}' were found."
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