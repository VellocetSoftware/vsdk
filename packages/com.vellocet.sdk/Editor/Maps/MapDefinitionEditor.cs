using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vellocet.Foundation.Editor;
using Vellocet.Sdk.Editor.Tools;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Maps
{
    [CustomEditor(typeof(MapDefinition))]
    public sealed class MapDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var map = (MapDefinition)target;
            var previousEnabled = map.Enabled;
            var previousEditorOnly = map.EditorOnly;
            using (new EditorGUI.DisabledScope(map.IsAddon))
            {
                using (new EditorGUI.DisabledScope(map.IsDraft))
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("<Enabled>k__BackingField"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("<EditorOnly>k__BackingField"));
                foreach (var name in new[] { "id", "displayName", "author", "version", "thumbnailReference" })
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
                if (ModeCatalog.TryResolveMapMode(map.id, out var mode))
                    EditorGUILayout.LabelField("Mode (from ID)", ModeCatalog.ResolveDisplayName(mode));
                else EditorGUILayout.HelpBox("Use a registered mode prefix in the ID, such as " +
                    string.Join(", ", ModeCatalog.All.Select(item => item.MapIdPrefix + "_example")), MessageType.Info);

                if (BaseMapAuthoring.IsVertexOwned(map))
                {
                    EditorGUILayout.HelpBox("This map is authored in Vertex. Save to Unity there to rebuild it.", MessageType.Info);
                    using (new EditorGUI.DisabledScope(!EditorCommandState.CanEdit || SdkHost.Actions?.DeveloperConnection == null))
                        if (GUILayout.Button("Open Vertex Map Editor…")) SdkEditorCommands.OpenVertex();
                }
                else
                {
                    var source = serializedObject.FindProperty("authoringScenePath");
                    var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(source.stringValue ?? "");
                    EditorGUI.BeginChangeCheck();
                    scene = (SceneAsset)EditorGUILayout.ObjectField("Authoring Scene", scene, typeof(SceneAsset), false);
                    if (EditorGUI.EndChangeCheck()) source.stringValue = scene ? AssetDatabase.GetAssetPath(scene) : "";
                }
            }
            serializedObject.ApplyModifiedProperties();
            if (map.IsDraft) EditorGUILayout.HelpBox("Draft. The first successful compile enables this map.", MessageType.Info);
            if (!map.IsAddon && !BaseMapAuthoring.TryValidateSource(map, out var error))
                EditorGUILayout.HelpBox(error, MessageType.Info);
            using (new EditorGUI.DisabledScope(!SdkEditorCommands.CanCompile(map)))
                if (GUILayout.Button("Compile Map")) { BaseMapAuthoring.Compile(map); GUIUtility.ExitGUI(); }
            if (!map.IsAddon && (previousEnabled != map.Enabled || previousEditorOnly != map.EditorOnly))
            {
                try { SdkHost.Actions?.BaseMaps?.Publish?.Invoke(map); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Compiled Scene", map.compiledScenePath ?? "");
                EditorGUILayout.TextField("Compilation Receipt", map.compilationReceiptPath ?? "");
                EditorGUILayout.TextField("Content Revision", map.contentRevision ?? "");
                EditorGUILayout.TextField("Compiled UTC", map.compiledUtc ?? "");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneReference"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("musicSet"));
            }
        }
    }

}
