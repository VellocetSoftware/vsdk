// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Audio;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Addons
{
    internal sealed class AddonAudioPackCreationRequest
    {
        internal string AssetFolder { get; set; } = AddonAuthoringAssetUtility.DefaultAddonsFolder;
        internal string AddonId { get; set; } = string.Empty;
        internal string Title { get; set; } = string.Empty;
        internal string Author { get; set; } = string.Empty;
        internal string Version { get; set; } = "1.0.0";
    }

    internal sealed class AddonAudioPackCreationResult
    {
        internal AddonAuthoringProject Project { get; set; }
        internal AddonAudioCollection Collection { get; set; }
        internal string AssetRoot { get; set; }
        internal string Error { get; set; }
        internal bool Success => Project && Collection && string.IsNullOrWhiteSpace(Error);
    }

    internal sealed class AddonAudioCueImportResult
    {
        internal AddonAudioCue[] Cues { get; set; } = Array.Empty<AddonAudioCue>();
        internal int SkippedCount { get; set; }
        internal string Error { get; set; }
        internal bool Success => string.IsNullOrWhiteSpace(Error);
    }

    internal static class AddonAuthoringAssetUtility
    {
        internal const string DefaultAddonsFolder = Constants.CustomContentRoot + "/Addons";
        internal const string DefaultAudioFolder = DefaultAddonsFolder + "/Audio";

        internal static AddonAuthoringProject CreateProject()
        {
            EnsureFolder(DefaultAddonsFolder);
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Add-on Project",
                "AddonProject",
                "asset",
                "Choose where to save the add-on authoring asset.",
                DefaultAddonsFolder);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var project = ScriptableObject.CreateInstance<AddonAuthoringProject>();
            AssetDatabase.CreateAsset(project, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            Selection.activeObject = project;
            EditorGUIUtility.PingObject(project);
            return project;
        }

        internal static bool TryValidateAudioPackRequest(AddonAudioPackCreationRequest request, out string error)
        {
            error = null;
            if (request == null)
            {
                error = "Audio pack request is missing.";
                return false;
            }

            if (!AddonIdPolicy.TryNormalize(request.AddonId, out _))
            {
                error = "Add-on ID must be a lowercase dotted ID such as 'author.audio_pack'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                error = "Title is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Author))
            {
                error = "Author is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Version))
            {
                error = "Version is required.";
                return false;
            }

            if (!TryNormalizeAssetFolder(request.AssetFolder, out var assetFolder))
            {
                error = "Asset location must be a folder below this Unity project's Assets folder.";
                return false;
            }

            var targetRoot = assetFolder + "/" + AddonIdPolicy.NormalizeOrEmpty(request.AddonId);
            if (AssetDatabase.IsValidFolder(targetRoot))
            {
                error =
                    $"An asset folder already exists for '{request.AddonId}'. Choose another add-on ID or location.";
                return false;
            }

            return true;
        }

        internal static AddonAudioPackCreationResult CreateAudioPack(AddonAudioPackCreationRequest request)
        {
            if (!TryValidateAudioPackRequest(request, out var error))
                return new AddonAudioPackCreationResult { Error = error };

            var addonId = AddonIdPolicy.NormalizeOrEmpty(request.AddonId);
            TryNormalizeAssetFolder(request.AssetFolder, out var assetFolder);
            var targetRoot = assetFolder + "/" + addonId;
            var audioFolder = targetRoot + "/Audio";
            var projectPath = targetRoot + "/AddonProject.asset";
            var collectionPath = audioFolder + "/AudioCollection.asset";
            AddonAuthoringProject project = null;
            AddonAudioCollection collection = null;

            try
            {
                EnsureFolder(audioFolder + "/Cues");

                project = ScriptableObject.CreateInstance<AddonAuthoringProject>();
                var projectObject = new SerializedObject(project);
                projectObject.FindProperty("addonId").stringValue = addonId;
                projectObject.FindProperty("title").stringValue = request.Title.Trim();
                projectObject.FindProperty("author").stringValue = request.Author?.Trim() ?? string.Empty;
                projectObject.FindProperty("version").stringValue = request.Version.Trim();
                projectObject.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(project, projectPath);

                collection = ScriptableObject.CreateInstance<AddonAudioCollection>();
                var collectionObject = new SerializedObject(collection);
                collectionObject.FindProperty("addonId").stringValue = addonId;
                collectionObject.FindProperty("contentId").stringValue =
                    ContentIdUtility.Sanitize(addonId + "_audio");
                collectionObject.FindProperty("title").stringValue = request.Title.Trim() + " Audio";
                collectionObject.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(collection, collectionPath);

                AddContentSource(project, collection);
                AssetDatabase.SaveAssets();
                Selection.activeObject = project;
                EditorGUIUtility.PingObject(project);
                return new AddonAudioPackCreationResult
                {
                    Project = project,
                    Collection = collection,
                    AssetRoot = targetRoot
                };
            }
            catch (Exception exception)
            {
                if (project && AssetDatabase.Contains(project))
                    AssetDatabase.DeleteAsset(projectPath);
                if (collection && AssetDatabase.Contains(collection))
                    AssetDatabase.DeleteAsset(collectionPath);
                if (AssetDatabase.IsValidFolder(targetRoot))
                    AssetDatabase.DeleteAsset(targetRoot);
                return new AddonAudioPackCreationResult
                {
                    Error = $"Could not create the audio pack assets: {exception.Message}"
                };
            }
        }

        internal static AddonAudioCollection CreateAudioCollection(AddonAuthoringProject project)
        {
            EnsureFolder(DefaultAudioFolder);
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Audio Collection",
                "AudioCollection",
                "asset",
                "Choose where to save the audio collection.",
                DefaultAudioFolder);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var collection = ScriptableObject.CreateInstance<AddonAudioCollection>();
            var serialized = new SerializedObject(collection);
            if (project)
            {
                serialized.FindProperty("addonId").stringValue = project.AddonId;
                serialized.FindProperty("contentId").stringValue =
                    ContentIdUtility.Sanitize(project.AddonId + "_audio");
                serialized.FindProperty("title").stringValue = project.Title + " Audio";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.CreateAsset(collection, AssetDatabase.GenerateUniqueAssetPath(path));
            if (project)
                AddContentSource(project, collection);
            AssetDatabase.SaveAssets();
            Selection.activeObject = collection;
            EditorGUIUtility.PingObject(collection);
            return collection;
        }

        internal static AddonAudioCue CreateAudioCue(AddonAuthoringProject project,
            AddonAudioCollection collection)
        {
            EnsureFolder(DefaultAudioFolder);
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Audio Cue",
                "AudioCue",
                "asset",
                "Choose where to save the audio cue.",
                DefaultAudioFolder);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var cue = ScriptableObject.CreateInstance<AddonAudioCue>();
            if (project)
            {
                var cueName = ContentIdUtility.Sanitize(Path.GetFileNameWithoutExtension(path));
                var serialized = new SerializedObject(cue);
                serialized.FindProperty("cueId").stringValue = project.AddonId + "." +
                                                               (string.IsNullOrWhiteSpace(cueName)
                                                                   ? "new_cue"
                                                                   : cueName);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.CreateAsset(cue, AssetDatabase.GenerateUniqueAssetPath(path));
            if (collection)
                AddCue(collection, cue);
            AssetDatabase.SaveAssets();
            Selection.activeObject = cue;
            EditorGUIUtility.PingObject(cue);
            return cue;
        }

        internal static AddonAudioCueImportResult CreateAudioCuesFromSelection(
            AddonAuthoringProject project,
            AddonAudioCollection collection,
            IEnumerable<Object> selection)
        {
            if (!project || !collection)
                return new AddonAudioCueImportResult { Error = "Select an Add-on Project and audio collection." };
            if (!string.Equals(project.AddonId, collection.AddonId, StringComparison.OrdinalIgnoreCase))
                return new AddonAudioCueImportResult { Error = "Audio collection owner does not match the project." };

            var audioPaths = ResolveSelectedAudioPaths(selection).ToArray();
            if (audioPaths.Length == 0)
                return new AddonAudioCueImportResult
                {
                    Error = "Select one or more imported WAV, Ogg Vorbis, or MP3 assets or folders."
                };

            var collectionPath = AssetDatabase.GetAssetPath(collection);
            if (string.IsNullOrWhiteSpace(collectionPath))
                return new AddonAudioCueImportResult { Error = "Save the audio collection as a project asset first." };

            var cueFolder = Path.GetDirectoryName(collectionPath)?.Replace('\\', '/') + "/Cues";
            EnsureFolder(cueFolder);
            var existingGuids = new HashSet<string>(collection.Cues.Where(value => value)
                .Select(value => value.SourceAssetGuid), StringComparer.OrdinalIgnoreCase);
            var cueIds = new HashSet<string>(collection.Cues.Where(value => value)
                .Select(value => value.CueId), StringComparer.OrdinalIgnoreCase);
            var created = new List<AddonAudioCue>();
            var skipped = 0;

            try
            {
                foreach (var audioPath in audioPaths)
                {
                    var guid = AssetDatabase.AssetPathToGUID(audioPath);
                    if (string.IsNullOrWhiteSpace(guid) || !existingGuids.Add(guid))
                    {
                        skipped++;
                        continue;
                    }

                    var baseSegment = NormalizeCueSegment(Path.GetFileNameWithoutExtension(audioPath));
                    var cueId = MakeUniqueCueId(project.AddonId, baseSegment, cueIds);
                    var cuePath = AssetDatabase.GenerateUniqueAssetPath(
                        cueFolder + "/" + NormalizeAssetFileName(baseSegment) + ".asset");
                    var cue = ScriptableObject.CreateInstance<AddonAudioCue>();
                    var cueObject = new SerializedObject(cue);
                    cueObject.FindProperty("cueId").stringValue = cueId;
                    cueObject.FindProperty("sourceAssetGuid").stringValue = guid;
                    cueObject.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(cue, cuePath);
                    AddCue(collection, cue);
                    created.Add(cue);
                }

                AssetDatabase.SaveAssets();
                if (created.Count > 0)
                {
                    Selection.objects = created.Cast<Object>().ToArray();
                    EditorGUIUtility.PingObject(created[0]);
                }

                return new AddonAudioCueImportResult { Cues = created.ToArray(), SkippedCount = skipped };
            }
            catch (Exception exception)
            {
                return new AddonAudioCueImportResult
                {
                    Cues = created.ToArray(),
                    SkippedCount = skipped,
                    Error = $"Could not create all audio cues: {exception.Message}"
                };
            }
        }

        internal static bool AddContentSource(AddonAuthoringProject project, Object source)
        {
            if (!project || !source || project.ContentSources.Contains(source))
                return false;

            Undo.RecordObject(project, "Add add-on content source");
            var serialized = new SerializedObject(project);
            var sources = serialized.FindProperty("contentSources");
            sources.InsertArrayElementAtIndex(sources.arraySize);
            sources.GetArrayElementAtIndex(sources.arraySize - 1).objectReferenceValue = source;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(project);
            return true;
        }

        private static void AddCue(AddonAudioCollection collection, AddonAudioCue cue)
        {
            if (!collection || !cue || collection.Cues.Contains(cue))
                return;

            Undo.RecordObject(collection, "Add audio cue");
            var serialized = new SerializedObject(collection);
            var cues = serialized.FindProperty("cues");
            cues.InsertArrayElementAtIndex(cues.arraySize);
            cues.GetArrayElementAtIndex(cues.arraySize - 1).objectReferenceValue = cue;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(collection);
        }

        private static IEnumerable<string> ResolveSelectedAudioPaths(IEnumerable<Object> selection)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var selected in selection ?? Array.Empty<Object>())
            {
                if (!selected)
                    continue;

                var path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                if (AssetDatabase.IsValidFolder(path))
                    foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { path }))
                    {
                        var audioPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (IsSupportedAudioAsset(audioPath))
                            paths.Add(audioPath);
                    }
                else if (selected is AudioClip && IsSupportedAudioAsset(path)) paths.Add(path);
            }

            return paths.OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsSupportedAudioAsset(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath) &&
                   AddonAudioPolicy.IsSupportedMediaExtension(Path.GetExtension(assetPath));
        }

        private static string MakeUniqueCueId(string addonId, string baseSegment, ISet<string> existing)
        {
            var suffix = baseSegment;
            var number = 2;
            var candidate = addonId + "." + suffix;
            while (existing.Contains(candidate))
            {
                suffix = baseSegment + "_" + number;
                candidate = addonId + "." + suffix;
                number++;
            }

            existing.Add(candidate);
            return candidate;
        }

        private static string NormalizeCueSegment(string value)
        {
            var output = new StringBuilder();
            var previousSeparator = false;
            foreach (var character in (value ?? string.Empty).Trim().ToLowerInvariant())
                if (char.IsLetterOrDigit(character) || character == '-')
                {
                    output.Append(character);
                    previousSeparator = false;
                }
                else if (!previousSeparator)
                {
                    output.Append('_');
                    previousSeparator = true;
                }

            var normalized = output.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(normalized) ? "new_cue" : normalized;
        }

        private static string NormalizeAssetFileName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "AudioCue" : value.Replace('-', '_');
        }

        internal static bool TryNormalizeAssetFolder(string folder, out string normalized)
        {
            normalized = (folder ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
            if (string.Equals(normalized, "Assets", StringComparison.Ordinal))
                return true;
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                return false;

            var segments = normalized.Split('/');
            return segments.All(segment => !string.IsNullOrWhiteSpace(segment) && segment != "." && segment != "..");
        }

        private static void EnsureFolder(string folder)
        {
            var normalized = folder.Replace('\\', '/').Trim('/');
            var segments = normalized.Split('/');
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException($"Authoring folder must be inside Assets: '{folder}'.");

            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
#endif