// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Audio;

namespace Vellocet.Sdk.Editor.Audio
{
    [CustomEditor(typeof(AddonAudioCue))]
    internal sealed class AddonAudioCueEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var originProperty = serializedObject.FindProperty("origin");
            EditorGUILayout.PropertyField(originProperty, new GUIContent("Media Owner"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cueId"), new GUIContent("Cue ID"));
            var isDependency = (AddonAudioCueOrigin)originProperty.enumValueIndex ==
                               AddonAudioCueOrigin.AddonDependency;
            if (isDependency)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dependencyAddonId"),
                    new GUIContent("Dependency Add-on ID",
                        "Add-on that supplies this cue's audio."));
                SdkEditorGui.DrawCallout(
                    "The map stores the cue ID and requires the provider add-on. " +
                    "Audio stays in the provider package.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("template"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gain"),
                    new GUIContent("Default Gain", "Per-cue gain applied before the player's Wwise bus volume."));

                var guidProperty = serializedObject.FindProperty("sourceAssetGuid");
                var sourcePath = AssetDatabase.GUIDToAssetPath(guidProperty.stringValue);
                var source = string.IsNullOrWhiteSpace(sourcePath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<AudioClip>(sourcePath);
                EditorGUI.BeginChangeCheck();
                var selected = (AudioClip)EditorGUILayout.ObjectField(
                    new GUIContent("Source Audio", "WAV, Ogg Vorbis, or MP3 copied into this add-on."),
                    source, typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    var selectedPath = selected ? AssetDatabase.GetAssetPath(selected) : string.Empty;
                    guidProperty.stringValue = string.IsNullOrWhiteSpace(selectedPath)
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(selectedPath);
                }

                var currentPath = AssetDatabase.GUIDToAssetPath(guidProperty.stringValue);
                if (!string.IsNullOrWhiteSpace(currentPath) &&
                    !AddonAudioPolicy.IsSupportedMediaExtension(Path.GetExtension(currentPath)))
                    SdkEditorGui.DrawCallout("Add-on audio sources must be WAV, Ogg Vorbis, or MP3 files.",
                        MessageType.Error);
                SdkEditorGui.DrawCallout(
                    "Use a lowercase cue ID owned by this add-on, such as author.package.wind_loop. " +
                    "This package includes the audio file; Wwise plays it in game.",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif