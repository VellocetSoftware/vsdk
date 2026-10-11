// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Editor.Workbench;

namespace Vellocet.Sdk.Editor.Audio
{
    [CustomEditor(typeof(AddonAudioCollection))]
    internal sealed class AddonAudioCollectionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("addonId"), new GUIContent("Add-on ID"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("contentId"),
                new GUIContent("Content ID", "Leave blank to derive it from the Add-on ID."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("title"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cues"), true);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            SdkEditorGui.DrawCallout(
                "Add this collection to an Add-on Project and build from SDK Workbench > Content. " +
                "Map builds include map-owned cues referenced by the scene.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Open Content tab", SdkEditorGui.PrimaryButton,
                        GUILayout.Height(30f)))
                    SdkWorkbenchWindow.OpenAddonContent(source: (AddonAudioCollection)target);
            }
        }
    }
}
#endif