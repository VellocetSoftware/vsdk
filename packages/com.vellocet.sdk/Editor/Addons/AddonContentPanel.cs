// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Editor.Tools;
using Vellocet.Sdk.Editor.VSig;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Addons
{
    internal sealed class AddonContentPanel
    {
        private const string PrefPrefix = "Vellocet.Sdk.ContentExporter.";
        private const string ExportRootPrefKey = PrefPrefix + "ExportRoot";
        private const string ProfilePrefKey = PrefPrefix + "Profile";
        private const string ProjectGuidPrefKey = PrefPrefix + "ProjectGuid";
        private string _exportRoot = string.Empty;
        private string _message = string.Empty;
        private MessageType _messageType = MessageType.Info;
        private AddonMapBuildProfile _profile = AddonMapBuildProfile.Development;

        private AddonAuthoringProject _project;

        internal void OnEnable()
        {
            _exportRoot = SdkPathDefaults.NormalizePath(EditorPrefs.GetString(ExportRootPrefKey, string.Empty));
            _profile = (AddonMapBuildProfile)EditorPrefs.GetInt(ProfilePrefKey,
                (int)AddonMapBuildProfile.Development);
            if (!Enum.IsDefined(typeof(AddonMapBuildProfile), _profile))
                _profile = AddonMapBuildProfile.Development;

            if (Selection.activeObject is AddonAuthoringProject selected)
            {
                SelectProject(selected);
                return;
            }

            var guid = EditorPrefs.GetString(ProjectGuidPrefKey, string.Empty);
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrWhiteSpace(path))
                SelectProject(AssetDatabase.LoadAssetAtPath<AddonAuthoringProject>(path));
        }

        internal void OnDisable()
        {
            EditorPrefs.SetString(ExportRootPrefKey, _exportRoot ?? string.Empty);
            EditorPrefs.SetInt(ProfilePrefKey, (int)_profile);
        }

        internal void SelectProject(AddonAuthoringProject project)
        {
            _project = project;
            if (!_project)
            {
                EditorPrefs.DeleteKey(ProjectGuidPrefKey);
                return;
            }

            var path = AssetDatabase.GetAssetPath(_project);
            EditorPrefs.SetString(ProjectGuidPrefKey, AssetDatabase.AssetPathToGUID(path));
        }

        internal void SelectSource(Object source)
        {
            if (!source)
                return;

            var containing = FindContainingProject(source);
            if (containing)
            {
                SelectProject(containing);
                _message = $"Selected '{containing.name}', which contains '{source.name}'.";
                _messageType = MessageType.Info;
                return;
            }

            _message = $"'{source.name}' is not in an Add-on Project. Select or create a project, then add it " +
                       "to Content Sources.";
            _messageType = MessageType.Warning;
        }

        internal void Draw()
        {
            DrawProject();
            EditorGUILayout.Space(8f);
            DrawContentRoutes();
            EditorGUILayout.Space(8f);
            DrawRuntimeReference();
            EditorGUILayout.Space(8f);
            DrawCreateActions();

            if (_project)
            {
                EditorGUILayout.Space(8f);
                DrawSourceStatus();
                EditorGUILayout.Space(8f);
                DrawDependencyReference();
                EditorGUILayout.Space(8f);
                DrawBuild();
            }

            if (!string.IsNullOrWhiteSpace(_message))
            {
                EditorGUILayout.Space(8f);
                SdkEditorGui.DrawCallout(_message, _messageType);
            }
        }

        private void DrawProject()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SdkEditorGui.DrawSectionHeader("Add-on Project",
                        "Package details and non-map content sources.");
                    GUILayout.FlexibleSpace();
                    SdkEditorGui.DrawBadge(_project ? "Selected" : "Required",
                        _project ? SdkEditorGui.Success : SdkEditorGui.Warning, 86f);
                }

                EditorGUILayout.Space(8f);
                EditorGUI.BeginChangeCheck();
                var selected = (AddonAuthoringProject)EditorGUILayout.ObjectField(
                    "Project", _project, typeof(AddonAuthoringProject), false);
                if (EditorGUI.EndChangeCheck())
                    SelectProject(selected);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create audio pack", SdkEditorGui.PrimaryButton,
                            GUILayout.Height(26f)))
                        AddonAudioPackWizard.Open(SelectProject);
                    if (GUILayout.Button("Create empty project", SdkEditorGui.SecondaryButton,
                            GUILayout.Height(26f)))
                        SelectProject(AddonAuthoringAssetUtility.CreateProject());
                    using (new EditorGUI.DisabledScope(!_project))
                    {
                        if (GUILayout.Button("Locate asset", SdkEditorGui.SecondaryButton,
                                GUILayout.Height(26f)))
                        {
                            Selection.activeObject = _project;
                            EditorGUIUtility.PingObject(_project);
                        }
                    }
                }

                if (!_project)
                {
                    EditorGUILayout.Space(6f);
                    SdkEditorGui.DrawCallout(
                        "Create an audio pack to get a project and collection. Use an empty project " +
                        "for other content.", MessageType.Info);
                    return;
                }

                EditorGUILayout.Space(10f);
                SdkEditorGui.DrawDivider();
                EditorGUILayout.Space(10f);
                var serialized = new SerializedObject(_project);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("addonId"),
                    new GUIContent("Add-on ID", "Stable lowercase dotted ID, such as author.package."));
                EditorGUILayout.PropertyField(serialized.FindProperty("title"));
                EditorGUILayout.PropertyField(serialized.FindProperty("author"));
                EditorGUILayout.PropertyField(serialized.FindProperty("version"));
                EditorGUILayout.PropertyField(serialized.FindProperty("addonThumbnail"),
                    new GUIContent("Thumbnail", "PNG or JPEG artwork for catalogs and publishing."));
                EditorGUILayout.PropertyField(serialized.FindProperty("requiredAddonIds"),
                    new GUIContent("Required Add-ons"), true);
                EditorGUILayout.PropertyField(serialized.FindProperty("contentSources"),
                    new GUIContent("Content Sources"), true);
                serialized.ApplyModifiedProperties();
            }
        }

        private static void DrawContentRoutes()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Where to build",
                    "Use the tool for your content type.");
                EditorGUILayout.Space(8f);
                SdkEditorGui.DrawInfoRow("Maps", "Export add-ons in Vertex; compile base maps in Unity");
                SdkEditorGui.DrawInfoRow("Audio pack", "Build cues and audio files from an Add-on Project");
                SdkEditorGui.DrawInfoRow("VSig", "Name the .vsig file after its scene; it builds with the map");
                SdkEditorGui.DrawInfoRow("Game resources", "Create and build standalone resources in VSDK");
                EditorGUILayout.Space(6f);
                SdkEditorGui.DrawCallout(
                    "Keep source outside generated package folders. Updating content preserves other declared entries.", MessageType.Info);
            }
        }

        private void DrawRuntimeReference()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Find reusable content",
                    "Find installed audio in the game's developer console.");
                EditorGUILayout.Space(8f);
                SdkEditorGui.DrawInfoRow("Runtime command", "audio_browser");
                SdkEditorGui.DrawInfoRow("Copy from browser", "Cue ID and Required add-on ID");
                EditorGUILayout.Space(7f);
                SdkEditorGui.DrawCallout(
                    "Use the add-on ID for requirements. The browser also shows the source, author, version, " +
                    "build ID, content ID and Workshop item.", MessageType.Info);
                EditorGUILayout.Space(7f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy audio_browser command", SdkEditorGui.SecondaryButton,
                            GUILayout.Height(28f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = "audio_browser";
                        _message = "Copied 'audio_browser'. Run it in the game's developer console.";
                        _messageType = MessageType.Info;
                    }

                    if (GUILayout.Button("Reusable content guide", SdkEditorGui.SecondaryButton,
                            GUILayout.Height(28f)))
                        SdkDocumentation.Open(SdkDocumentationPage.ReusableContent);
                }
            }
        }

        private void DrawCreateActions()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Create assets",
                    "Add audio or scripts to your project.");
                EditorGUILayout.Space(8f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SdkEditorGui.DrawBigButton("Audio collection",
                            _project ? "Create and add it to this project." : "Create a collection asset.",
                            primary: true))
                    {
                        var collection = AddonAuthoringAssetUtility.CreateAudioCollection(_project);
                        if (collection)
                        {
                            _message = _project
                                ? $"Created '{collection.name}' and added it to this project."
                                : $"Created '{collection.name}'. Add it to an Add-on Project before building.";
                            _messageType = _project ? MessageType.Info : MessageType.Warning;
                        }
                    }

                    GUILayout.Space(8f);
                    if (SdkEditorGui.DrawBigButton("Audio cue",
                            "Create a cue, then assign its source file."))
                    {
                        var collections = _project
                            ? _project.ContentSources.OfType<AddonAudioCollection>().ToArray()
                            : Array.Empty<AddonAudioCollection>();
                        var collection = collections.Length == 1 ? collections[0] : null;
                        var cue = AddonAuthoringAssetUtility.CreateAudioCue(_project, collection);
                        if (cue)
                        {
                            _message = collection
                                ? $"Created '{cue.name}' and added it to '{collection.name}'."
                                : $"Created '{cue.name}'. Add it to an audio collection after assigning media.";
                            _messageType = collection ? MessageType.Info : MessageType.Warning;
                        }
                    }
                }

                EditorGUILayout.Space(8f);
                if (GUILayout.Button("Create cues from selected audio or folders", SdkEditorGui.PrimaryButton,
                        GUILayout.Height(30f)))
                    CreateCuesFromSelection();
                EditorGUILayout.Space(5f);
                EditorGUILayout.LabelField(
                    "Select WAV, Ogg Vorbis or MP3 assets or folders in the Project window. " +
                    "Creates one cue per file, skipping files already in the collection.",
                    SdkEditorGui.Muted);

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SdkEditorGui.DrawBigButton("VSig script", "Create a .vsig logic source."))
                        VSigAssetCreationMenu.CreateVSigScript();
                    GUILayout.Space(8f);
                    if (SdkEditorGui.DrawBigButton("VSig library", "Create a reusable .vsiglib source."))
                        VSigAssetCreationMenu.CreateVSigLibraryScript();
                    GUILayout.Space(8f);
                    if (SdkEditorGui.DrawBigButton("Map in Vertex", "Author maps and export add-ons in Vertex."))
                        Workbench.SdkWorkbenchWindow.Open();
                }
            }
        }

        private void DrawDependencyReference()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Use this content in another add-on",
                    "Add this package ID to the consuming add-on's requirements.");
                if (GUILayout.Button("Copy requirement ID", SdkEditorGui.SecondaryButton, GUILayout.Height(28f)))
                {
                    EditorGUIUtility.systemCopyBuffer = _project.AddonId;
                    _message = $"Copied '{_project.AddonId}'.";
                    _messageType = MessageType.Info;
                }
            }
        }

        private void DrawSourceStatus()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Content check",
                    "Fix source errors before building.");
                EditorGUILayout.Space(8f);

                if (_project.ContentSources.Length == 0)
                {
                    SdkEditorGui.DrawCallout("Add at least one content source to this project.",
                        MessageType.Warning);
                    return;
                }

                var context = new AddonContentExportContext(_project, string.Empty,
                    _profile == AddonMapBuildProfile.Release);
                foreach (var source in _project.ContentSources)
                {
                    IAddonContentSourceExporter exporter = null;
                    string resolveError = null;
                    string sourceError = null;
                    var ready = false;
                    if (source && AddonContentSourceExporterRegistry.TryResolve(source, out exporter,
                            out resolveError))
                        ready = exporter.TryValidate(source, context, out sourceError);
                    var detail = !source
                        ? "Missing asset"
                        : ready
                            ? exporter.DisplayName
                            : !string.IsNullOrWhiteSpace(resolveError)
                                ? resolveError
                                : sourceError;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(source, typeof(Object), false,
                            GUILayout.MinWidth(180f));
                        EditorGUILayout.LabelField(detail, SdkEditorGui.Muted, GUILayout.ExpandWidth(true));
                        SdkEditorGui.DrawBadge(ready ? "Ready" : "Needs work",
                            ready ? SdkEditorGui.Success : SdkEditorGui.Danger, 88f);
                    }

                    EditorGUILayout.Space(3f);
                }

                var audioCollections = _project.ContentSources.OfType<AddonAudioCollection>().ToArray();
                if (audioCollections.Any(value => !string.Equals(value.AddonId, _project.AddonId,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    EditorGUILayout.Space(5f);
                    if (GUILayout.Button("Sync audio owner IDs", SdkEditorGui.SecondaryButton,
                            GUILayout.Height(26f)))
                        SyncAudioOwnerIds(audioCollections);
                }
            }
        }

        private void DrawBuild()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Build content add-on",
                    "Build and check the package before replacing the previous output.");
                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Package output root");
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField(string.IsNullOrWhiteSpace(_exportRoot)
                            ? "Not selected"
                            : _exportRoot);
                    }

                    if (GUILayout.Button(string.IsNullOrWhiteSpace(_exportRoot) ? "Choose…" : "Change…",
                            SdkEditorGui.SecondaryButton, GUILayout.Width(82f), GUILayout.Height(22f)))
                        ChooseExportFolder();
                }

                EditorGUILayout.Space(5f);
                if (GUILayout.Button("Use existing add-on package…", SdkEditorGui.SecondaryButton,
                        GUILayout.Height(26f)))
                    ChooseExistingPackage();

                EditorGUILayout.Space(8f);
                _profile = (AddonMapBuildProfile)SdkEditorGui.DrawSegmentedControl((int)_profile,
                    new[] { "Development", "Release" });
                EditorPrefs.SetInt(ProfilePrefKey, (int)_profile);
                EditorGUILayout.Space(8f);
                SdkEditorGui.DrawCallout(_profile == AddonMapBuildProfile.Release
                    ? "Release requires a thumbnail and must pass publishing checks."
                    : "Development packages are for local testing and cannot be published.");

                EditorGUILayout.Space(8f);
                var finalRoot = AddonContentBuildService.ResolveAddonRoot(_project, _exportRoot);
                SdkEditorGui.DrawInfoRow("Package folder",
                    string.IsNullOrWhiteSpace(finalRoot) ? "Choose a package output root" : finalRoot);
                SdkEditorGui.DrawInfoRow("Destination", DescribeDestination(finalRoot));
                SdkEditorGui.DrawInfoRow("Sources", _project.ContentSources.Length.ToString());
                SdkEditorGui.DrawInfoRow("Generated manifest",
                    "buildId, createdUtc, versions, catalogs, file sizes, and SHA-256 hashes");

                EditorGUILayout.Space(10f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var request = BuildRequest();
                    var requestReady = AddonContentBuildService.TryValidateRequest(request, out _);
                    using (new EditorGUI.DisabledScope(!requestReady || EditorApplication.isCompiling))
                    {
                        if (GUILayout.Button(_profile == AddonMapBuildProfile.Release
                                    ? "Build release package"
                                    : "Build development package",
                                SdkEditorGui.PrimaryButton, GUILayout.Height(32f)))
                            Build(request);
                    }

                    if (GUILayout.Button("Validate output", SdkEditorGui.SecondaryButton,
                            GUILayout.Height(32f)))
                        ValidateOutput();
                    if (GUILayout.Button("Documentation", SdkEditorGui.SecondaryButton,
                            GUILayout.Height(32f)))
                        SdkDocumentation.Open(SdkDocumentationPage.ContentAddons);
                }

                var validationRequest = BuildRequest();
                if (!AddonContentBuildService.TryValidateRequest(validationRequest, out var validationError))
                {
                    EditorGUILayout.Space(7f);
                    SdkEditorGui.DrawCallout(validationError, MessageType.Warning);
                }
            }
        }

        private AddonContentBuildRequest BuildRequest()
        {
            return new AddonContentBuildRequest
            {
                Project = _project,
                PackageOutputRoot = _exportRoot,
                Profile = _profile
            };
        }

        private void ChooseExportFolder()
        {
            var selected = EditorUtility.OpenFolderPanel("Choose Package Output Root",
                SdkPathDefaults.GetPackageOutputPickerStartPath(_exportRoot), string.Empty);
            if (string.IsNullOrWhiteSpace(selected))
                return;

            if (!SdkPackageOutputPathPolicy.TryResolveWritable(selected, out var normalized, out var error))
            {
                EditorUtility.DisplayDialog("Invalid Package Output Root", error, "OK");
                return;
            }

            _exportRoot = normalized;
            EditorPrefs.SetString(ExportRootPrefKey, _exportRoot);
        }

        private void ChooseExistingPackage()
        {
            var selected = EditorUtility.OpenFolderPanel("Choose Existing Add-on Package",
                SdkPathDefaults.GetPackageOutputPickerStartPath(_exportRoot), string.Empty);
            if (string.IsNullOrWhiteSpace(selected))
                return;

            if (!AddonHelper.TryLoad(selected, out var manifest, out var error))
            {
                EditorUtility.DisplayDialog("Invalid Add-on Package", error, "OK");
                return;
            }

            if (!string.Equals(manifest.id, _project.AddonId, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Add-on ID Does Not Match",
                    $"The selected package is '{manifest.id}', but this project is '{_project.AddonId}'.", "OK");
                return;
            }

            var parent = Directory.GetParent(Path.GetFullPath(selected));
            var pathError = string.Empty;
            if (parent == null || !SdkPackageOutputPathPolicy.TryResolveWritable(parent.FullName,
                    out var normalized,
                    out pathError))
            {
                EditorUtility.DisplayDialog("Invalid Package Location",
                    pathError ?? "The package parent folder is unavailable.", "OK");
                return;
            }

            _exportRoot = normalized;
            EditorPrefs.SetString(ExportRootPrefKey, _exportRoot);
            _message =
                $"Selected existing '{manifest.title}' package. The builder will preserve its map and VMod entries.";
            _messageType = MessageType.Info;
        }

        private void Build(AddonContentBuildRequest request)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Content Add-on", "Building and validating package…", 0.5f);
                var result = AddonContentBuildService.Build(request);
                _message = result.Success
                    ? $"Built '{result.Manifest.title}' (build {result.BuildId}). " +
                      $"Package passed validation:\n{result.AddonRoot}"
                    : result.Error;
                _messageType = result.Success ? MessageType.Info : MessageType.Error;
                if (result.Success)
                    EditorUtility.RevealInFinder(result.AddonRoot);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ValidateOutput()
        {
            var addonRoot = AddonContentBuildService.ResolveAddonRoot(_project, _exportRoot);
            var mode = _profile == AddonMapBuildProfile.Release
                ? AddonPackageValidationMode.Release
                : AddonPackageValidationMode.Standard;
            if (AddonPackageValidator.TryValidate(addonRoot, mode, out var manifest, out var error))
            {
                _message = $"Package '{manifest.id}' is valid for the {_profile.ToString().ToLowerInvariant()} " +
                           "profile.";
                _messageType = MessageType.Info;
            }
            else
            {
                _message = error;
                _messageType = MessageType.Error;
            }
        }

        private void SyncAudioOwnerIds(AddonAudioCollection[] collections)
        {
            foreach (var collection in collections.Where(value => value))
            {
                Undo.RecordObject(collection, "Sync audio owner ID");
                var serialized = new SerializedObject(collection);
                serialized.FindProperty("addonId").stringValue = _project.AddonId;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(collection);
            }

            AssetDatabase.SaveAssets();
            _message = "Audio collection owner IDs now match the Add-on Project.";
            _messageType = MessageType.Info;
        }

        private void CreateCuesFromSelection()
        {
            if (!_project)
            {
                _message = "Create or select an Add-on Project first.";
                _messageType = MessageType.Warning;
                return;
            }

            var collections = _project.ContentSources.OfType<AddonAudioCollection>().Where(value => value).ToArray();
            if (collections.Length != 1)
            {
                _message = collections.Length == 0
                    ? "Create an audio collection before importing cues."
                    : "Bulk import needs exactly one audio collection in the project. Import into each collection separately.";
                _messageType = MessageType.Warning;
                return;
            }

            var result = AddonAuthoringAssetUtility.CreateAudioCuesFromSelection(
                _project, collections[0], Selection.objects);
            if (!result.Success)
            {
                _message = result.Error;
                _messageType = MessageType.Error;
                return;
            }

            _message = result.Cues.Length == 0
                ? $"No cues were created. Skipped {result.SkippedCount} file(s) already in the collection."
                : $"Created {result.Cues.Length} cue(s) in '{collections[0].name}'. " +
                  $"Skipped {result.SkippedCount} existing file(s).";
            _messageType = MessageType.Info;
        }

        private static string DescribeDestination(string addonRoot)
        {
            if (string.IsNullOrWhiteSpace(addonRoot))
                return "Not selected";
            if (!Directory.Exists(addonRoot))
                return "New package";
            if (!AddonHelper.TryLoad(addonRoot, out var manifest, out _))
                return "Existing package needs attention";

            var kinds = (manifest.contents ?? Array.Empty<AddonManifest.ContentEntry>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.kind))
                .Select(value => value.kind.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return kinds.Length == 0 ? "Existing package" : "Existing package: " + string.Join(", ", kinds);
        }

        private static AddonAuthoringProject FindContainingProject(Object source)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AddonAuthoringProject"))
            {
                var project = AssetDatabase.LoadAssetAtPath<AddonAuthoringProject>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (project && project.ContentSources.Contains(source))
                    return project;
            }

            return null;
        }
    }
}
#endif
