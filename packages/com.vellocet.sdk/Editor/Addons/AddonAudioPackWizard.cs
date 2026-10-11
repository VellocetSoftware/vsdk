// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;

namespace Vellocet.Sdk.Editor.Addons
{
    internal sealed class AddonAudioPackWizard : SdkBrandedEditorWindow
    {
        private const string AuthorPrefKey = "Vellocet.Sdk.AudioPackWizard.Author";

        [SerializeField] private string _addonId = string.Empty;
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private string _author = string.Empty;
        [SerializeField] private string _version = "1.0.0";
        [SerializeField] private string _assetFolder = AddonAuthoringAssetUtility.DefaultAddonsFolder;
        [SerializeField] private Vector2 _scroll;
        private Action<AddonAuthoringProject> _onCreated;

        private void OnEnable()
        {
            ReloadSdkBrandingAssets();
            if (string.IsNullOrWhiteSpace(_author))
                _author = EditorPrefs.GetString(AuthorPrefKey, string.Empty);
        }

        private void OnGUI()
        {
            SdkEditorGui.DrawWindowBackground(position);
            DrawSdkHeader("Vellocet SDK", "New audio pack",
                "Create a project and its first audio collection.", 96f);

            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;
            EditorGUILayout.Space(16f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(18f);
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawIdentity();
                    EditorGUILayout.Space(8f);
                    DrawOwnership();
                    EditorGUILayout.Space(8f);
                    DrawLocation();
                    EditorGUILayout.Space(12f);
                    DrawActions();
                }

                GUILayout.Space(18f);
            }

            EditorGUILayout.Space(12f);
        }

        internal static void Open(Action<AddonAuthoringProject> onCreated = null)
        {
            var window = GetWindow<AddonAudioPackWizard>(true, "Create Audio Pack", true);
            window.minSize = new Vector2(620f, 590f);
            window._onCreated = onCreated;
            window.Show();
            window.Focus();
        }

        private void DrawIdentity()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Package identity",
                    "Keep the add-on ID unchanged after publishing.");
                EditorGUILayout.Space(8f);
                _addonId = EditorGUILayout.TextField(new GUIContent("Add-on ID", "Example: ada.announcer_pack"),
                    _addonId);
                _title = EditorGUILayout.TextField("Title", _title);
                _author = EditorGUILayout.TextField("Author", _author);
                _version = EditorGUILayout.TextField("Version", _version);
            }
        }

        private static void DrawOwnership()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Generated files",
                    "Add your artwork, audio, cue settings and dependencies.");
                EditorGUILayout.Space(8f);
                SdkEditorGui.DrawInfoRow("Generated", "addon.json, audio.json, content entries, and file hashes");
                SdkEditorGui.DrawInfoRow("Per build", "buildId, createdUtc, SDK version, and Unity version");
                SdkEditorGui.DrawInfoRow("Release", "distribution profile and release policy");
                EditorGUILayout.Space(7f);
                SdkEditorGui.DrawCallout(
                    "Edit source assets, then rebuild to update these files.",
                    MessageType.Info);
            }
        }

        private void DrawLocation()
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Unity asset location",
                    "One folder holds the project, collection and cues.");
                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Parent folder");
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField(_assetFolder);
                    }

                    if (GUILayout.Button("Choose…", SdkEditorGui.SecondaryButton,
                            GUILayout.Width(82f), GUILayout.Height(22f)))
                        ChooseAssetFolder();
                }

                var normalizedId = AddonIdPolicy.NormalizeOrEmpty(_addonId);
                SdkEditorGui.DrawInfoRow("Creates", string.IsNullOrWhiteSpace(normalizedId)
                    ? _assetFolder + "/<add-on-id>"
                    : _assetFolder + "/" + normalizedId);
            }
        }

        private void DrawActions()
        {
            var request = Request();
            var ready = AddonAuthoringAssetUtility.TryValidateAudioPackRequest(request, out var error);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", SdkEditorGui.SecondaryButton, GUILayout.Height(34f)))
                    Close();
                using (new EditorGUI.DisabledScope(!ready))
                {
                    if (GUILayout.Button("Create audio pack", SdkEditorGui.PrimaryButton, GUILayout.Height(34f)))
                        Create(request);
                }
            }

            if (!ready)
            {
                EditorGUILayout.Space(7f);
                SdkEditorGui.DrawCallout(error, MessageType.Warning);
            }
        }

        private AddonAudioPackCreationRequest Request()
        {
            return new AddonAudioPackCreationRequest
            {
                AssetFolder = _assetFolder,
                AddonId = _addonId,
                Title = _title,
                Author = _author,
                Version = _version
            };
        }

        private void Create(AddonAudioPackCreationRequest request)
        {
            var result = AddonAuthoringAssetUtility.CreateAudioPack(request);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("Audio Pack Not Created", result.Error, "OK");
                return;
            }

            EditorPrefs.SetString(AuthorPrefKey, request.Author?.Trim() ?? string.Empty);
            _onCreated?.Invoke(result.Project);
            Close();
        }

        private void ChooseAssetFolder()
        {
            var currentAbsolute = Path.GetFullPath(_assetFolder);
            var selected = EditorUtility.OpenFolderPanel("Choose Unity Asset Folder", currentAbsolute, string.Empty);
            if (string.IsNullOrWhiteSpace(selected))
                return;

            var relative = FileUtil.GetProjectRelativePath(selected).Replace('\\', '/').TrimEnd('/');
            if (!AddonAuthoringAssetUtility.TryNormalizeAssetFolder(relative, out var normalized))
            {
                EditorUtility.DisplayDialog("Invalid Asset Folder",
                    "Choose a folder below this Unity project's Assets folder.", "OK");
                return;
            }

            _assetFolder = normalized;
        }
    }
}
#endif