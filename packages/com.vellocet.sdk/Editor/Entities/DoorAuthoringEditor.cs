using System;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Doors;
using Vertex.Unity.Editor;
using UnityEngine;
using Vellocet.Sdk.Entities;

namespace Vellocet.Sdk.Editor.Entities
{
    [InitializeOnLoad]
    internal static class DoorAuthoringEditor
    {
        private static DoorMarker _preview;
        private static Transform _body;
        private static DoorPose _closed;
        private static DoorMotion _motion;
        private static double _started;
        private static float _progress;
        private static GameObject _ghost;
        private static readonly List<(Renderer Renderer, bool Hidden)> HiddenRenderers = new();
        private static readonly List<Mesh> BakedMeshes = new();

        static DoorAuthoringEditor()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.playModeStateChanged += _ => StopPreview();
            EditorApplication.quitting += StopPreview;
            Selection.selectionChanged += StopPreview;
            SceneView.duringSceneGui += DrawHandles;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (_, _) => StopPreview();
        }

        internal static void Draw(DoorMarker marker)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(_preview == marker ? "Stop preview" : "Preview open / close"))
                    {
                        if (_preview == marker)
                            StopPreview();
                        else
                            StartPreview(marker);
                    }
                }
                var body = (Transform)EditorGUILayout.ObjectField("Moving geometry", marker.BrushTransform, typeof(Transform), true);
                if (body != marker.BrushTransform && body && (body == marker.transform || body.IsChildOf(marker.transform)))
                {
                    StopPreview();
                    Undo.RecordObject(marker, "Change door geometry");
                    marker.SetBody(body == marker.transform ? null : body);
                    EditorUtility.SetDirty(marker);
                }
            }
            if (!marker.Sounds && GUILayout.Button("Create door sound set"))
            {
                var path = EditorUtility.SaveFilePanelInProject("Create door sound set", "Door sounds", "asset", "Choose where to save this sound set.");
                if (!string.IsNullOrEmpty(path))
                {
                    var sounds = ScriptableObject.CreateInstance<DoorSoundSet>();
                    AssetDatabase.CreateAsset(sounds, path);
                    using var soundData = new SerializedObject(marker);
                    soundData.FindProperty("<Sounds>k__BackingField").objectReferenceValue = sounds;
                    soundData.ApplyModifiedProperties();
                }
            }
            if (marker.Sounds && GUILayout.Button("Edit door sound set"))
                AssetDatabase.OpenAsset(marker.Sounds);
            if (marker.OverrideSounds)
                foreach (DoorSoundAction action in Enum.GetValues(typeof(DoorSoundAction)))
                {
                    var overridden = marker.TryGetSoundOverride(action, out var current);
                    EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(action.ToString()), !overridden ? "Sound set" : current.IsEmpty ? "Silent" : current.Cue ? current.Cue.name : current.Event);
                    var source = EditorGUILayout.ObjectField("Assign event / cue", null, typeof(UnityEngine.Object), false);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Silence"))
                        {
                            Undo.RecordObject(marker, "Silence door action");
                            marker.SetSoundOverride(action, default);
                            EditorUtility.SetDirty(marker);
                        }
                        using (new EditorGUI.DisabledScope(!overridden))
                            if (GUILayout.Button("Use sound set"))
                            {
                                Undo.RecordObject(marker, "Reset door action");
                                marker.RemoveSoundOverride(action);
                                EditorUtility.SetDirty(marker);
                            }
                    }
                    if (source)
                    {
                        var sound = DoorSoundAuthoring.Reference(source);
                        Undo.RecordObject(marker, "Change door sound");
                        marker.SetSoundOverride(action, sound);
                        EditorUtility.SetDirty(marker);
                    }
                }
            var settingsType = SdkGame.Current.DoorSettingsComponentType;
            if (settingsType == null)
                return;
            var settings = marker.GetComponent(settingsType);
            if (!settings)
            {
                if (GUILayout.Button("Add " + SdkGame.Current.DisplayName + " interaction / property settings"))
                    Undo.AddComponent(marker.gameObject, settingsType);
                return;
            }
            EditorGUILayout.LabelField(SdkGame.Current.DisplayName, EditorStyles.boldLabel);
            using var data = new SerializedObject(settings);
            data.Update();
            var options = data.FindProperty("options");
            if (options != null)
            {
                var field = options.Copy();
                var end = options.GetEndProperty();
                for (var next = field.NextVisible(true); next && !SerializedProperty.EqualContents(field, end); next = field.NextVisible(false))
                {
                    if (field.name == "property" && options.FindPropertyRelative("isProperty")?.boolValue != true)
                        continue;
                    EditorGUILayout.PropertyField(field, true);
                }
                var property = options.FindPropertyRelative("property");
                if (options.FindPropertyRelative("isProperty")?.boolValue == true && property != null)
                {
                    if (!property.objectReferenceValue && SdkGame.Current.DoorPropertyMarkerType is { } propertyType && GUILayout.Button("Create property"))
                    {
                        var root = new GameObject("Property - " + marker.name);
                        Undo.RegisterCreatedObjectUndo(root, "Create door property");
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, marker.gameObject.scene);
                        root.transform.position = marker.transform.position;
                        property.objectReferenceValue = Undo.AddComponent(root, propertyType);
                    }
                    if (property.objectReferenceValue)
                    {
                        using var record = new SerializedObject(property.objectReferenceValue);
                        record.Update();
                        var setting = record.GetIterator();
                        if (setting.NextVisible(true))
                            while (setting.NextVisible(false))
                            {
                                if (setting.name is "guid" or "ignoreNavMesh" or "ignoreFromNavMeshInitialized" or "vertexAuthoredGuid")
                                    continue;
                                EditorGUILayout.PropertyField(setting, true);
                            }
                        record.ApplyModifiedProperties();
                    }
                }
            }
            data.ApplyModifiedProperties();
        }

        private static void StartPreview(DoorMarker marker)
        {
            StopPreview();
            if (!DoorGeometry.TryGetLocalBounds(marker.BrushTransform, marker.BrushTransform, marker, out var bounds))
                return;
            _preview = marker;
            _body = marker.BrushTransform;
            _closed = DoorPose.Capture(_body);
            try
            {
                _motion = new DoorMotion(marker, _closed, bounds);
            }
            catch (Exception) { _preview = null; _body = null; throw; }
            _ghost = new GameObject("Door movement preview") { hideFlags = HideFlags.HideAndDontSave };
            _ghost.transform.SetParent(_body.parent, false);
            _closed.Apply(_ghost.transform);
            var lowerLods = new HashSet<Renderer>();
            foreach (var group in _body.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                for (var level = 1; level < lods.Length; level++)
                    foreach (var item in lods[level].renderers)
                        if (item)
                            lowerLods.Add(item);
            }
            foreach (var renderer in _body.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<EntityMarker>(true) != marker)
                    continue;
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh();
                    skinned.BakeMesh(mesh);
                    BakedMeshes.Add(mesh);
                }
                else if (renderer.TryGetComponent<MeshFilter>(out var filter))
                    mesh = filter.sharedMesh;
                if (!mesh)
                    continue;
                HiddenRenderers.Add((renderer, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
                if (lowerLods.Contains(renderer))
                    continue;
                var copy = new GameObject(renderer.name) { hideFlags = HideFlags.HideAndDontSave };
                copy.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                copy.transform.localScale = renderer.transform.lossyScale;
                copy.transform.SetParent(_ghost.transform, true);
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                var presentation = copy.AddComponent<MeshRenderer>();
                presentation.sharedMaterials = renderer.sharedMaterials;
                presentation.enabled = renderer.enabled && renderer.gameObject.activeInHierarchy;
                presentation.shadowCastingMode = renderer.shadowCastingMode;
                presentation.receiveShadows = renderer.receiveShadows;
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                presentation.SetPropertyBlock(properties);
            }
            _started = EditorApplication.timeSinceStartup;
            EditorApplication.update += TickPreview;
        }

        private static void TickPreview()
        {
            if (!_preview || !_body || !_ghost)
            {
                StopPreview();
                return;
            }
            var phase = Mathf.Repeat((float)(EditorApplication.timeSinceStartup - _started) / Mathf.Max(.1f, _motion.Duration), 2);
            _progress = phase <= 1 ? DoorMotion.Curve(phase, _preview.OpeningEase) : 1 - DoorMotion.Curve(phase - 1, _preview.ClosingEase);
            _motion.Evaluate(_progress).Apply(_ghost.transform);
            SceneView.RepaintAll();
        }

        private static void StopPreview()
        {
            EditorApplication.update -= TickPreview;
            foreach (var item in HiddenRenderers)
                if (item.Renderer)
                    item.Renderer.forceRenderingOff = item.Hidden;
            HiddenRenderers.Clear();
            if (_ghost)
                UnityEngine.Object.DestroyImmediate(_ghost);
            _ghost = null;
            foreach (var mesh in BakedMeshes)
                if (mesh)
                    UnityEngine.Object.DestroyImmediate(mesh);
            BakedMeshes.Clear();
            _preview = null;
            _body = null;
            SceneView.RepaintAll();
        }

        private static void DrawHandles(SceneView scene)
        {
            if (_preview || Selection.activeGameObject == null || !Selection.activeGameObject.TryGetComponent<DoorMarker>(out var marker))
                return;
            var body = marker.BrushTransform;
            using var data = new SerializedObject(marker);
            var pivot = body.TransformPoint(marker.Pivot);
            if (marker.MotionKind != DoorMotionKind.Sliding)
            {
                EditorGUI.BeginChangeCheck();
                var next = Handles.PositionHandle(pivot, body.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    data.FindProperty("<Pivot>k__BackingField").vector3Value = body.InverseTransformPoint(next);
                    data.ApplyModifiedProperties();
                }
            }
            EditorGUI.BeginChangeCheck();
            if (marker is RotatingDoorMarker rotating)
            {
                var turn = Handles.RotationHandle(body.rotation * Quaternion.AngleAxis(rotating.Angle, rotating.RotationAxis.normalized), pivot);
                if (EditorGUI.EndChangeCheck())
                {
                    var relative = Quaternion.Inverse(body.rotation) * turn;
                    relative.ToAngleAxis(out var angle, out var axis);
                    data.FindProperty("<Angle>k__BackingField").floatValue = Vector3.Dot(axis, rotating.RotationAxis) < 0 ? -angle : angle;
                    data.ApplyModifiedProperties();
                }
            }
            else if (marker is ScalingDoorMarker scaling && DoorGeometry.TryGetLocalBounds(body, body, marker, out var bounds))
            {
                var axis = scaling.Axis == ScalingDoorMarker.ScaleAxis.X ? Vector3.right : scaling.Axis == ScalingDoorMarker.ScaleAxis.Y ? Vector3.up : Vector3.forward;
                var basis = body.parent ? body.parent.TransformVector(body.localRotation * axis) : body.localRotation * axis;
                var extent = Mathf.Max(.01f, bounds.size[(int)scaling.Axis]) * basis.magnitude;
                var endpoint = Handles.Slider(pivot + basis.normalized * (scaling.TargetScale * extent), basis.normalized);
                if (EditorGUI.EndChangeCheck())
                {
                    data.FindProperty("<TargetScale>k__BackingField").floatValue = Mathf.Max(0, Vector3.Dot(endpoint - pivot, basis.normalized) / extent);
                    data.ApplyModifiedProperties();
                }
            }
            else if (marker.MotionKind == DoorMotionKind.Sliding && DoorGeometry.TryGetLocalBounds(body, body, marker, out bounds))
            {
                var motion = new DoorMotion(marker, DoorPose.Capture(body), bounds);
                var parent = body.parent;
                var open = motion.Evaluate(1).Position;
                var endpoint = parent ? parent.TransformPoint(open) : open;
                var direction = marker.BrushTransform == marker.transform ? body.TransformDirection(marker.GetLocalOpenDirection()) : marker.transform.TransformDirection(marker.GetLocalOpenDirection());
                var moved = Handles.Slider(endpoint, direction);
                if (EditorGUI.EndChangeCheck())
                {
                    var local = parent ? parent.InverseTransformPoint(moved) : moved;
                    var localDirection = parent ? parent.InverseTransformDirection(direction) : direction;
                    data.FindProperty("<UseTravelDistance>k__BackingField").boolValue = true;
                    data.FindProperty("<TravelDistance>k__BackingField").floatValue = Vector3.Dot(local - body.localPosition, localDirection.normalized);
                    data.ApplyModifiedProperties();
                }
            }
            else
                EditorGUI.EndChangeCheck();
        }
    }
}
