// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Editor.Addons;
using Vellocet.Framework.Editor.Spatial;
using Vellocet.Sdk.Editor.Tools;

namespace Vellocet.Sdk.Editor.Workbench
{
    public sealed class SdkWorkbenchWindow : SdkBrandedEditorWindow
    {
        private const string WindowTitle = "SDK Workbench";
        [SerializeField] private AuthorWorkspaceTab _authorWorkspace;
        [SerializeField] private Vector2 _scroll;
        [SerializeField] private bool _spatial;
        private AddonContentPanel _addonContentPanel;
        private SpatialOctreePreviewPanel _spatialPanel;

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(660, 460);
            ReloadSdkBrandingAssets();
            if ((int)_authorWorkspace > 2) _authorWorkspace = AuthorWorkspaceTab.Maps;
        }

        private void OnDisable()
        {
            _addonContentPanel?.OnDisable(); _spatialPanel?.OnDisable();
            _addonContentPanel = null;
            _spatialPanel = null;
        }

        private void OnGUI()
        {
            SdkEditorGui.DrawWindowBackground(position);
            DrawSdkHeader("Vellocet SDK", "Game integration", "Author Unity scenes or Vertex maps and build add-ons.", 90);
            if (!SdkUnityVersionGuard.IsSupported)
            { EditorGUILayout.HelpBox(SdkUnityVersionGuard.RequirementMessage, MessageType.Error); return; }
            _authorWorkspace = (AuthorWorkspaceTab)GUILayout.Toolbar((int)_authorWorkspace, new[] { "Maps", "Content", "Diagnostics" });
            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;
            using var body = new EditorGUILayout.VerticalScope();
            EditorGUILayout.Space(16);
            switch (_authorWorkspace)
            {
                case AuthorWorkspaceTab.Maps:
                    EditorGUILayout.LabelField("Author a Unity scene", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Create a Map Definition, assign its Authoring Scene and a prefixed ID, then Compile Map. Keep one map per folder.", MessageType.Info);
                    using (new EditorGUI.DisabledScope(!Vellocet.Foundation.Editor.EditorCommandState.CanEdit))
                        if (GUILayout.Button("Create Map Definition", GUILayout.Height(34))) Tools.SdkEditorCommands.CreateMapDefinition();
                    EditorGUILayout.Space(16);
                    EditorGUILayout.LabelField("Make maps in Vertex", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Open or create a map, then choose Build → Export add-on. Game developers can use Save to Unity to add it to the base game.", MessageType.Info);
                    if (Tools.SdkHost.Actions?.DeveloperConnection != null)
                    {
                        using (new EditorGUI.DisabledScope(!Vellocet.Foundation.Editor.EditorCommandState.CanEdit))
                            if (GUILayout.Button("Open Vertex", GUILayout.Height(34))) Tools.SdkEditorCommands.OpenVertex();
                    }
                    else EditorGUILayout.LabelField("Launch Vertex from your game's SDK.", EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("SDK documentation")) SdkDocumentation.OpenHome();
                    break;
                case AuthorWorkspaceTab.Content:
                    EnsureAddonContentPanel(); _addonContentPanel.Draw(); break;
                case AuthorWorkspaceTab.Diagnostics:
                    EditorGUILayout.HelpBox("Inspect compiled maps. Validate reports problems without repairing assets; Prepare Game Content applies preparation.", MessageType.None);
                    using (new EditorGUI.DisabledScope(!Vellocet.Foundation.Editor.EditorCommandState.CanEdit))
                        if (GUILayout.Button("Validate Project")) Vellocet.Foundation.Editor.GameEditorCommands.ValidateProject();
                    _spatial = EditorGUILayout.Foldout(_spatial, "Spatial inspection", true);
                    if (_spatial) { EnsureSpatialPanel(); _spatialPanel.DrawPreviewWorkspace(); }
                    break;
            }
        }

        private void EnsureAddonContentPanel()
        { if (_addonContentPanel != null) return; _addonContentPanel = new AddonContentPanel(); _addonContentPanel.OnEnable(); }
        private void EnsureSpatialPanel()
        { if (_spatialPanel != null) return; _spatialPanel = new SpatialOctreePreviewPanel(); _spatialPanel.OnEnable(this); }

        [MenuItem("Tools/Vellocet/SDK/Workbench", priority = 500)]
        public static void Open()
        {
            var window = GetWindow<SdkWorkbenchWindow>(WindowTitle);
            window._authorWorkspace = AuthorWorkspaceTab.Maps;
        }
        internal static void OpenAddonContent(AddonAuthoringProject project = null, UnityEngine.Object source = null)
        {
            var window = GetWindow<SdkWorkbenchWindow>(WindowTitle);
            window._authorWorkspace = AuthorWorkspaceTab.Content;
            window.EnsureAddonContentPanel();
            if (project) window._addonContentPanel.SelectProject(project);
            if (source) window._addonContentPanel.SelectSource(source);
            window.Repaint();
        }
        private enum AuthorWorkspaceTab { Maps, Content, Diagnostics }
    }
}
