// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Editor.Workbench;

namespace Vellocet.Sdk.Editor.Addons
{
    [CustomEditor(typeof(AddonAuthoringProject))]
    internal sealed class AddonAuthoringProjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("addonId"),
                new GUIContent("Add-on ID", "Stable lowercase dotted ID, such as author.package."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("title"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("author"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("version"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("addonThumbnail"),
                new GUIContent("Add-on Thumbnail", "PNG or JPEG shown in catalogs and Workshop."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredAddonIds"),
                new GUIContent("Required Add-ons"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("contentSources"),
                new GUIContent("Content Sources"), true);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            SdkEditorGui.DrawCallout(
                "Package details, dependencies, artwork and non-map " +
                "content sources. Build from SDK Workbench > Content.", MessageType.Info);
            if (GUILayout.Button("Open Content tab", SdkEditorGui.PrimaryButton, GUILayout.Height(30f)))
                SdkWorkbenchWindow.OpenAddonContent((AddonAuthoringProject)target);
        }
    }
}
#endif