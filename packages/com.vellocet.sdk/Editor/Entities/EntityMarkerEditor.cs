// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vertex.VSig;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Editor.VSig;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Logging;
using Vellocet.Sdk.VSig;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Entities
{
    [CustomEditor(typeof(EntityMarker), true)]
    [CanEditMultipleObjects]
    internal sealed class EntityMarkerEditor : UnityEditor.Editor
    {
        private const string GuidPropertyName = "guid";
        private const string IgnoreNavMeshPropertyName = "ignoreNavMesh";
        private const string RotateAnglePropertyName = "<Angle>k__BackingField";
        private const string RotateLoopPropertyName = "<LoopContinuously>k__BackingField";
        private const string RotateResetDelayPropertyName = "<ResetDelay>k__BackingField";
        private const string RotateReversePropertyName = "<ReverseDirection>k__BackingField";
        private const string RotateStartActivePropertyName = "<StartActive>k__BackingField";
        private const string RotateStartOpenPropertyName = "<StartOpen>k__BackingField";
        private const string RotateStartPhasePropertyName = "<StartPhase>k__BackingField";
        private const string ShowVSigSectionPrefKey = "Vellocet.Sdk.MarkerEditor.ShowVSigSection";
        private const string ShowVSigContractPrefKey = "Vellocet.Sdk.MarkerEditor.ShowVSigContract";
        private const string ShowVSigIoPrefKey = "Vellocet.Sdk.MarkerEditor.ShowVSigIo";
        private const int SnapRaycastCapacity = 128;
        private const float VSigRowHeight = 21f;
        private const float VSigRowInsetX = 8f;
        private const float VSigRowInsetY = 2f;
        private const float VSigRowInsetWidth = 16f;
        private const float VSigRowColumnGap = 6f;
        private const float VSigBadgeWidth = 78f;
        private const float VSigBadgeInsetY = 1f;
        private const float VSigMinNameWidth = 72f;
        private const float VSigMinTypeVisibleWidth = 34f;
        private const float VSigMinIoAccessWidth = 38f;
        private const float VSigCompactAccessThreshold = 56f;
        private static readonly RaycastHit[] SnapRaycastHits = new RaycastHit[SnapRaycastCapacity];

        private static readonly string[] BaseProperties =
        {
            "m_Script",
            GuidPropertyName,
            IgnoreNavMeshPropertyName,
            "ignoreFromNavMeshInitialized"
        };

        private bool _showAdvanced;
        private bool _showVSigContract;
        private bool _showVSigIo;
        private bool _showVSigSection;

        private void OnEnable()
        {
            _showVSigSection = EditorPrefs.GetBool(ShowVSigSectionPrefKey, true);
            _showVSigContract = EditorPrefs.GetBool(ShowVSigContractPrefKey, true);
            _showVSigIo = EditorPrefs.GetBool(ShowVSigIoPrefKey, true);
        }

        public bool HasFrameBounds()
        {
            return target is EntityMarker marker && EntityAvatarPreview.TryGetBounds(marker, out _);
        }

        public Bounds OnGetFrameBounds()
        {
            var bounds = new Bounds(((EntityMarker)target).transform.position, Vector3.zero);
            var first = true;
            foreach (var selected in targets)
            {
                if (selected is not EntityMarker marker)
                    continue;
                if (!EntityAvatarPreview.TryGetBounds(marker, out var candidate))
                    candidate = new Bounds(marker.transform.position, Vector3.one);
                if (first)
                    bounds = candidate;
                else
                    bounds.Encapsulate(candidate);
                first = false;
            }
            return bounds;
        }

        public override void OnInspectorGUI()
        {
            if (target is not EntityMarker marker)
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();
            EntityDefinitionCatalog.TryGetDefinition(marker.GetType(), out var definition);

            DrawCompactHeader(marker, definition);
            DrawQuickActions(marker);
            DrawReference(marker, definition);
            DrawMovingBrushGeometry(marker);
            DrawVSigAuthoring(marker);
            DrawMarkerSpecificProperties(marker);
            if (marker is DoorMarker door) DoorAuthoringEditor.Draw(door);
            if (marker is LightMarker lightMarker) DrawLight(lightMarker);
            DrawAdvanced(marker);

            var changed = serializedObject.ApplyModifiedProperties();
            if (changed)
                ApplyMarkerSpecificAuthoring();
        }

        private static void DrawCompactHeader(EntityMarker marker, EntityDefinitionCatalog.Definition definition)
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            using (new EditorGUILayout.HorizontalScope())
            {
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(marker.IconPath);
                var iconRect = GUILayoutUtility.GetRect(42f, 42f, GUILayout.Width(42f), GUILayout.Height(42f));
                EditorGUI.DrawRect(iconRect, SdkEditorStyle.IconFrameBackground);
                if (icon)
                {
                    var padded = new Rect(iconRect.x + 5f, iconRect.y + 5f, iconRect.width - 10f,
                        iconRect.height - 10f);
                    GUI.DrawTexture(padded, icon, ScaleMode.ScaleToFit, true);
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(marker.name, HeaderTitleStyle());
                    var className = definition?.EntityClassName ?? marker.EntityClassName ?? marker.GetType().Name;
                    var displayName = definition?.DisplayName ?? marker.GetType().Name;
                    EditorGUILayout.LabelField($"{displayName}  |  {className}", SdkEditorGui.Muted);
                }
            }
        }

        private void DrawQuickActions(EntityMarker marker)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping", EditorStyles.miniButtonLeft))
                    EditorGUIUtility.PingObject(marker);

                if (GUILayout.Button("Open VSig", EditorStyles.miniButtonMid))
                    OpenSceneVSig(marker);

                if (GUILayout.Button("Duplicate", EditorStyles.miniButtonMid))
                    DuplicateMarker(marker);

                if (GUILayout.Button("Snap Ground", EditorStyles.miniButtonRight))
                    SnapMarkersToGround();
            }
        }

        private void DrawReference(EntityMarker marker, EntityDefinitionCatalog.Definition definition)
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                var multi = targets.Length > 1;
                SdkEditorGui.DrawSectionHeader("Reference",
                    multi
                        ? "Shared reference fields for the selected markers."
                        : "Stable marker reference used by VSig and map binding.");
                EditorGUILayout.Space(4f);

                SdkEditorGui.DrawInfoRow("Entity class",
                    multi
                        ? DescribeSelectedEntityClasses()
                        : definition?.EntityClassName ?? marker.EntityClassName ?? "<unset>");

                DrawGameObjectNameField(marker);

                var ignoreNavMesh = serializedObject.FindProperty(IgnoreNavMeshPropertyName);
                if (ignoreNavMesh != null)
                    ignoreNavMesh.boolValue = EditorGUILayout.Toggle(
                        new GUIContent("Ignore NavMesh",
                            "Exclude this marker from NavMesh builds."),
                        ignoreNavMesh.boolValue);
            }
        }

        private void DrawGameObjectNameField(EntityMarker marker)
        {
            var selectedMarkers = CollectSelectedMarkers();
            if (selectedMarkers.Count == 0)
                selectedMarkers.Add(marker);

            var firstName = selectedMarkers[0] && selectedMarkers[0].gameObject
                ? selectedMarkers[0].gameObject.name
                : string.Empty;
            var mixed = false;
            var isFirst = true;
            foreach (var selected in selectedMarkers)
            {
                if (isFirst)
                {
                    isFirst = false;
                    continue;
                }

                var selectedName = selected && selected.gameObject ? selected.gameObject.name : string.Empty;
                if (string.Equals(firstName, selectedName, StringComparison.Ordinal))
                    continue;

                mixed = true;
                break;
            }

            var content = new GUIContent("Target Name (GameObject)",
                "Primary VSig target name. Duplicate names of the same entity class fan out as a group.");

            EditorGUI.BeginChangeCheck();
            var previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mixed;
            var newName = EditorGUILayout.TextField(content, firstName);
            EditorGUI.showMixedValue = previousMixedValue;
            if (!EditorGUI.EndChangeCheck())
                return;

            RenameSelectedGameObjects(selectedMarkers, newName);
        }

        private void DrawMovingBrushGeometry(EntityMarker marker)
        {
            if (marker is not IMovingBrushEntityMarker movingBrush || !movingBrush.BrushTransform)
                return;

            var brushRoot = movingBrush.BrushTransform;
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader(
                    "Moving brush geometry",
                    "Child renderers and colliders move together as one predicted solid.");
                EditorGUILayout.Space(4f);

                SdkEditorGui.DrawInfoRow(
                    "Renderers",
                    brushRoot.GetComponentsInChildren<Renderer>(true).Length.ToString());
                SdkEditorGui.DrawInfoRow(
                    "Colliders",
                    brushRoot.GetComponentsInChildren<Collider>(true).Length.ToString());

                SdkEditorGui.DrawCallout(
                    "Place game prop prefabs under this marker. Props keep their shared assets and separate meshes.",
                    MessageType.Info);

                if (targets.Length != 1)
                {
                    EditorGUILayout.LabelField(
                        "Select one moving brush to add geometry.",
                        SdkEditorGui.Muted);
                    return;
                }

                var dropRect = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
                GUI.Box(
                    dropRect,
                    "Drop prop prefab assets or scene GameObjects here",
                    EditorStyles.helpBox);
                HandleMovingBrushGeometryDrop(marker, brushRoot, dropRect);
            }
        }

        private static void HandleMovingBrushGeometryDrop(
            EntityMarker marker,
            Transform brushRoot,
            Rect dropRect)
        {
            var current = Event.current;
            if (current == null || !dropRect.Contains(current.mousePosition))
                return;

            if (current.type == EventType.DragUpdated)
            {
                if (HasAdoptableMovingBrushGeometry(marker, brushRoot))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    current.Use();
                }

                return;
            }

            if (current.type != EventType.DragPerform ||
                !HasAdoptableMovingBrushGeometry(marker, brushRoot))
                return;

            DragAndDrop.AcceptDrag();
            var adopted = 0;
            foreach (var dragged in DragAndDrop.objectReferences)
                if (TryAdoptMovingBrushGeometry(marker, brushRoot, dragged))
                    adopted++;

            if (adopted > 0)
                NormalizeMovingBrushGeometry(brushRoot);

            current.Use();
        }

        private static bool HasAdoptableMovingBrushGeometry(EntityMarker marker, Transform brushRoot)
        {
            foreach (var dragged in DragAndDrop.objectReferences)
                if (CanAdoptMovingBrushGeometry(marker, brushRoot, dragged))
                    return true;

            return false;
        }

        private static bool TryAdoptMovingBrushGeometry(
            EntityMarker marker,
            Transform brushRoot,
            Object dragged)
        {
            if (!CanAdoptMovingBrushGeometry(marker, brushRoot, dragged))
                return false;

            var source = ResolveDraggedGameObject(dragged);
            if (EditorUtility.IsPersistent(source))
            {
                var instance = PrefabUtility.InstantiatePrefab(source, brushRoot) as GameObject;
                if (!instance)
                    return false;

                Undo.RegisterCreatedObjectUndo(instance, "Add Moving Brush Geometry");
                return true;
            }

            Undo.SetTransformParent(source.transform, brushRoot, "Add Moving Brush Geometry");
            return true;
        }

        private static bool CanAdoptMovingBrushGeometry(
            EntityMarker marker,
            Transform brushRoot,
            Object dragged)
        {
            if (!marker || !brushRoot)
                return false;

            var source = ResolveDraggedGameObject(dragged);
            if (!source)
                return false;

            if (EditorUtility.IsPersistent(source))
                return PrefabUtility.GetPrefabAssetType(source) != PrefabAssetType.NotAPrefab;

            if (!source.scene.IsValid() || source.scene != marker.gameObject.scene)
                return false;

            var sourceTransform = source.transform;
            if (sourceTransform == brushRoot ||
                sourceTransform.IsChildOf(brushRoot) ||
                brushRoot.IsChildOf(sourceTransform))
                return false;

            return !PrefabUtility.IsPartOfPrefabInstance(source) ||
                   PrefabUtility.IsAnyPrefabInstanceRoot(source);
        }

        private static GameObject ResolveDraggedGameObject(Object dragged)
        {
            return dragged switch
            {
                GameObject gameObject => gameObject,
                Component component => component.gameObject,
                _ => null
            };
        }

        private static void NormalizeMovingBrushGeometry(Transform brushRoot)
        {
            var transforms = brushRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var current = transforms[i];
                if (!current || GameObjectUtility.GetStaticEditorFlags(current.gameObject) == 0)
                    continue;

                Undo.RecordObject(current.gameObject, "Normalize Moving Brush Geometry");
                GameObjectUtility.SetStaticEditorFlags(current.gameObject, 0);
                EditorUtility.SetDirty(current.gameObject);
            }

            var renderers = brushRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer || !renderer.staticShadowCaster)
                    continue;

                Undo.RecordObject(renderer, "Normalize Moving Brush Geometry");
                renderer.staticShadowCaster = false;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void RenameSelectedGameObjects(IReadOnlyList<EntityMarker> markers, string newName)
        {
            if (markers == null || markers.Count == 0)
                return;

            var gameObjects = new List<GameObject>(markers.Count);
            var seen = new HashSet<int>();
            foreach (var marker in markers)
            {
                if (!marker || !marker.gameObject)
                    continue;

                var go = marker.gameObject;
                if (seen.Add(go.GetInstanceID()))
                    gameObjects.Add(go);
            }

            if (gameObjects.Count == 0)
                return;

            var objects = new Object[gameObjects.Count];
            for (var i = 0; i < gameObjects.Count; i++)
                objects[i] = gameObjects[i];

            Undo.RecordObjects(objects, gameObjects.Count > 1 ? "Rename Entity Markers" : "Rename Entity Marker");
            foreach (var gameObject in gameObjects)
            {
                gameObject.name = newName;
                EditorUtility.SetDirty(gameObject);
            }
        }

        private void DrawMarkerSpecificProperties(EntityMarker marker)
        {
            if (!HasMarkerSpecificProperties())
                return;

            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                SdkEditorGui.DrawSectionHeader("Marker settings", "Values stored on this marker.");
                EditorGUILayout.Space(4f);

                var iterator = serializedObject.GetIterator();
                var enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (IsBaseProperty(iterator.propertyPath))
                        continue;
                    if (!ShouldDrawMarkerSpecificProperty(marker, iterator))
                        continue;

                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
        }

        private bool ShouldDrawMarkerSpecificProperty(EntityMarker marker, SerializedProperty property)
        {
            if (!marker.ShowAuthoringProperty(property.name)) return false;
            if (marker is RotateMarker)
                return ShouldDrawRotateProperty(property);

            return true;
        }

        private void DrawLight(LightMarker marker)
        {
            var lights = new Object[targets.Length];
            for (var i = 0; i < targets.Length; i++) lights[i] = ((LightMarker)targets[i]).GetComponent<Light>();
            using var properties = new SerializedObject(lights);
            properties.Update();
            var type = properties.FindProperty("m_Type");
            EditorGUI.showMixedValue = type.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUILayout.IntPopup("Light type", type.intValue,
                new[] { "Point", "Spot", "Rectangle", "Disc" }, new[] { (int)LightType.Point, (int)LightType.Spot, (int)LightType.Rectangle, (int)LightType.Disc });
            if (EditorGUI.EndChangeCheck()) type.intValue = selected;
            EditorGUI.showMixedValue = false;
            foreach (var name in new[] { "m_Color", "m_Intensity", "m_Range", "m_SpotAngle", "m_AreaSize", "m_Enabled", "m_Cookie" })
                EditorGUILayout.PropertyField(properties.FindProperty(name));
            var shadows = properties.FindProperty("m_Shadows.m_Type");
            EditorGUI.showMixedValue = shadows.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var cast = EditorGUILayout.Toggle("Cast shadows", shadows.intValue != (int)LightShadows.None);
            if (EditorGUI.EndChangeCheck()) shadows.intValue = (int)(cast ? LightShadows.Soft : LightShadows.None);
            EditorGUI.showMixedValue = false;
            if (properties.ApplyModifiedProperties())
                foreach (var value in targets) ((LightMarker)value).ApplyLighting();
            EditorGUILayout.HelpBox("Effects and scripting change Baked lights to Dynamic. Mixed keeps baked indirect light. Area lights are baked only, without effects or scripting.", MessageType.Info);
        }

        private bool ShouldDrawRotateProperty(SerializedProperty property)
        {
            var loop = serializedObject.FindProperty(RotateLoopPropertyName);
            if (loop == null || loop.hasMultipleDifferentValues)
                return true;

            if (loop.boolValue)
                return property.name != RotateAnglePropertyName &&
                       property.name != RotateResetDelayPropertyName &&
                       property.name != RotateStartOpenPropertyName;

            return property.name != RotateStartPhasePropertyName &&
                   property.name != RotateStartActivePropertyName &&
                   property.name != RotateReversePropertyName;
        }

        private void ApplyMarkerSpecificAuthoring()
        {
            for (var i = 0; i < targets.Length; i++)
                ((EntityMarker)targets[i]).ApplyAuthoringChanges();
        }

        private void DrawVSigAuthoring(EntityMarker marker)
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                var multi = targets.Length > 1;
                var contract = multi ? null : marker.SignalContract;
                var header = multi
                    ? $"VSig ({targets.Length} selected)"
                    : $"VSig ({contract?.Outputs?.Count ?? 0} out, {contract?.Inputs?.Count ?? 0} in)";

                _showVSigSection = EditorGUILayout.Foldout(_showVSigSection, header, true);
                EditorPrefs.SetBool(ShowVSigSectionPrefKey, _showVSigSection);
                if (!_showVSigSection)
                    return;

                EditorGUILayout.Space(3f);

                DrawVSigActions(marker);
                if (!multi)
                {
                    EditorGUILayout.Space(2f);
                    DrawVSigContractSection(contract);
                    EditorGUILayout.Space(2f);
                    DrawVSigIoSection(marker, contract);
                }
            }
        }

        private void DrawVSigContractSection(EntitySignalContract contract)
        {
            var outputCount = contract?.Outputs?.Count ?? 0;
            var inputCount = contract?.Inputs?.Count ?? 0;
            var propertyCount = contract?.Properties?.Count ?? 0;
            var stateCount = contract?.States?.Count ?? 0;
            var totalCount = outputCount + inputCount + propertyCount + stateCount;
            _showVSigContract = EditorGUILayout.Foldout(_showVSigContract,
                $"Contract Members ({totalCount})", true);
            EditorPrefs.SetBool(ShowVSigContractPrefKey, _showVSigContract);
            if (!_showVSigContract)
                return;

            DrawSignalMemberGroup("Outputs", contract?.Outputs);
            DrawSignalMemberGroup("Inputs", contract?.Inputs);
            DrawSignalMemberGroup("Properties", contract?.Properties);
            DrawSignalMemberGroup("State", contract?.States);
        }

        private void DrawVSigIoSection(EntityMarker marker, EntitySignalContract contract)
        {
            var outputCount = contract?.Outputs?.Count ?? 0;
            var inputCount = contract?.Inputs?.Count ?? 0;
            _showVSigIo = EditorGUILayout.Foldout(_showVSigIo, $"I/O ({outputCount} out, {inputCount} in)", true);
            EditorPrefs.SetBool(ShowVSigIoPrefKey, _showVSigIo);
            if (!_showVSigIo)
                return;

            DrawVSigIoDetails(marker, contract);
        }

        private static void DrawSignalMemberGroup(string title, IReadOnlyList<EntitySignalMember> members)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField($"{title} ({CountSignalMembers(members)})", EditorStyles.boldLabel);

            if (members == null || members.Count == 0)
            {
                EditorGUILayout.LabelField("  None", SdkEditorGui.Muted);
                return;
            }

            DrawMemberColumnHeader();
            foreach (var member in members)
            {
                if (member == null || string.IsNullOrWhiteSpace(member.Name))
                    continue;

                var accessColor = AccessColor(member);
                var rowRect = EditorGUILayout.GetControlRect(false, VSigRowHeight);
                DrawMemberRowBackground(rowRect, accessColor);

                var contentRect = BuildRowContentRect(rowRect);
                BuildContractRowColumns(contentRect, out var nameRect, out var typeRect, out var badgeRect);

                GUI.Label(nameRect, member.Name, SdkEditorGui.Body);
                if (typeRect.width >= VSigMinTypeVisibleWidth)
                    GUI.Label(typeRect, MemberTypeName(member), SdkEditorGui.Caption);
                DrawAccessBadge(badgeRect, MemberAccessText(member), accessColor);
            }
        }

        private static int CountSignalMembers(IReadOnlyList<EntitySignalMember> members)
        {
            if (members == null || members.Count == 0)
                return 0;

            var count = 0;
            foreach (var member in members)
                if (member != null && !string.IsNullOrWhiteSpace(member.Name))
                    count++;

            return count;
        }

        private static string MemberTypeName(EntitySignalMember member)
        {
            return string.IsNullOrWhiteSpace(member.TypeName)
                ? member.ValueType.ToString().ToLowerInvariant()
                : member.TypeName;
        }

        private static string MemberAccessText(EntitySignalMember member)
        {
            if (member.Readable && member.Writable)
                return "read/write";
            if (member.Readable)
                return "read";
            return member.Writable ? "write" : "internal";
        }

        private static string CompactAccessText(EntitySignalMember member, bool compact)
        {
            if (!compact)
                return MemberAccessText(member);
            if (member.Readable && member.Writable)
                return "r/w";
            if (member.Readable)
                return "r";
            return member.Writable ? "w" : "int";
        }

        private static Color AccessColor(EntitySignalMember member)
        {
            if (member.Readable && member.Writable)
                return SdkEditorStyle.Success;
            if (member.Readable)
                return SdkEditorStyle.Accent;
            return member.Writable ? SdkEditorStyle.Warning : SdkEditorStyle.MutedText;
        }

        private static void DrawMemberColumnHeader()
        {
            var headerRect = EditorGUILayout.GetControlRect(false, 16f);
            var contentRect = new Rect(headerRect.x + VSigRowInsetX, headerRect.y, headerRect.width - VSigRowInsetWidth,
                headerRect.height);
            var nameRect = new Rect(contentRect.x, contentRect.y, contentRect.width * 0.5f, contentRect.height);
            var typeRect = new Rect(nameRect.xMax + VSigRowColumnGap, contentRect.y, contentRect.width * 0.26f,
                contentRect.height);
            var accessRect = new Rect(contentRect.xMax - VSigBadgeWidth, contentRect.y, VSigBadgeWidth,
                contentRect.height);

            GUI.Label(nameRect, "Name", SdkEditorGui.Caption);
            GUI.Label(typeRect, "Type", SdkEditorGui.Caption);
            GUI.Label(accessRect, "Access", RightAlignedCaption());
        }

        private static void DrawMemberRowBackground(Rect rect, Color accent)
        {
            EditorGUI.DrawRect(rect, SdkEditorStyle.SubtleBackground);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), SdkEditorStyle.WithAlpha(accent, 0.9f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), SdkEditorStyle.Divider);
        }

        private static void DrawAccessBadge(Rect rect, string text, Color color)
        {
            var fill = SdkEditorStyle.BadgeFill(color);
            EditorGUI.DrawRect(rect, fill);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), SdkEditorStyle.WithAlpha(color, 0.95f));
            var labelStyle = CenteredMiniBoldLabel();
            labelStyle.normal.textColor = SdkEditorStyle.ContrastText(fill);
            GUI.Label(rect, text, labelStyle);
        }

        private static GUIStyle RightAlignedCaption()
        {
            var style = new GUIStyle(SdkEditorGui.Caption);
            style.alignment = TextAnchor.MiddleRight;
            return style;
        }

        private static GUIStyle CenteredMiniBoldLabel()
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel);
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        private void DrawVSigActions(EntityMarker marker)
        {
            var selectedMarkers = CollectSelectedMarkers();
            var canGroup = selectedMarkers.Count >= 2;
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                var refLabel = targets.Length > 1 ? "Add Selected Refs" : "Add Ref";
                if (GUILayout.Button(refLabel, EditorStyles.miniButtonLeft))
                    AddSelectedRefsToSceneVSig(marker);

                using (new EditorGUI.DisabledScope(!canGroup))
                {
                    var groupContent = new GUIContent("Add Group",
                        "Create a VSig group from two or more selected markers.");
                    if (GUILayout.Button(groupContent, EditorStyles.miniButtonMid))
                        AddSelectedGroupToSceneVSig(marker);
                }

                if (GUILayout.Button("Copy Ref", EditorStyles.miniButtonRight))
                    CopyRef(marker);
            }

            if (!canGroup)
                EditorGUILayout.LabelField("Select two or more markers to create a VSig group.", SdkEditorGui.Muted);
        }

        private static void DrawVSigIoDetails(EntityMarker marker, EntitySignalContract contract)
        {
            TryGetRouteDetails(marker, out var outputRoutesBySignal, out var inputRoutesBySignal, out var error);

            DrawIoGroup("Outputs", contract?.Outputs, SdkEditorStyle.Accent, outputRoutesBySignal);
            DrawIoGroup("Inputs", contract?.Inputs, SdkEditorStyle.AccentWarm, inputRoutesBySignal);

            if (!string.IsNullOrWhiteSpace(error))
                EditorGUILayout.LabelField(error, SdkEditorGui.Muted);
        }

        private static void DrawIoGroup(string title,
            IReadOnlyList<EntitySignalMember> members,
            Color color, Dictionary<string, List<string>> routesBySignal)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            if (members == null || members.Count == 0)
            {
                EditorGUILayout.LabelField("  None", SdkEditorGui.Muted);
                return;
            }

            foreach (var member in members)
            {
                if (member == null || string.IsNullOrWhiteSpace(member.Name))
                    continue;

                var normalized = EntitySignalNameUtility.Normalize(member.Name);
                routesBySignal ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                routesBySignal.TryGetValue(normalized, out var routes);
                var routeCount = routes?.Count ?? 0;

                var rowRect = EditorGUILayout.GetControlRect(false, VSigRowHeight);
                DrawMemberRowBackground(rowRect, color);
                var contentRect = BuildRowContentRect(rowRect);
                BuildIoRowColumns(contentRect, out var nameRect, out var typeRect, out var accessRect,
                    out var routesRect);

                GUI.Label(nameRect, member.Name, SdkEditorGui.Body);
                if (typeRect.width >= VSigMinTypeVisibleWidth)
                    GUI.Label(typeRect, MemberTypeName(member), SdkEditorGui.Caption);
                if (accessRect.width > 0f)
                    GUI.Label(accessRect, CompactAccessText(member, accessRect.width < VSigCompactAccessThreshold),
                        SdkEditorGui.Muted);
                DrawAccessBadge(routesRect, routeCount == 0 ? "0 routes" : $"{routeCount} routes",
                    SdkEditorStyle.Accent);

                if (routeCount == 0)
                    continue;

                foreach (var route in routes)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(24f);
                        GUILayout.Label($"- {route}", SdkEditorGui.Muted);
                    }
            }
        }

        private static Rect BuildRowContentRect(Rect rowRect)
        {
            return new Rect(rowRect.x + VSigRowInsetX, rowRect.y + VSigRowInsetY, rowRect.width - VSigRowInsetWidth,
                rowRect.height - VSigRowInsetY * 2f);
        }

        private static Rect BuildRightBadgeRect(Rect contentRect)
        {
            return new Rect(contentRect.xMax - VSigBadgeWidth, contentRect.y + VSigBadgeInsetY, VSigBadgeWidth,
                contentRect.height - VSigBadgeInsetY * 2f);
        }

        private static void BuildContractRowColumns(Rect contentRect, out Rect nameRect, out Rect typeRect,
            out Rect badgeRect)
        {
            badgeRect = BuildRightBadgeRect(contentRect);
            var leftWidth = Mathf.Max(0f, badgeRect.x - contentRect.x - VSigRowColumnGap);
            var nameWidth = leftWidth < VSigMinNameWidth
                ? leftWidth
                : Mathf.Clamp(leftWidth * 0.62f, VSigMinNameWidth, leftWidth);
            var typeWidth = Mathf.Max(0f, leftWidth - nameWidth - VSigRowColumnGap);

            nameRect = new Rect(contentRect.x, contentRect.y, nameWidth, contentRect.height);
            typeRect = new Rect(nameRect.xMax + VSigRowColumnGap, contentRect.y, typeWidth, contentRect.height);
        }

        private static void BuildIoRowColumns(Rect contentRect, out Rect nameRect, out Rect typeRect,
            out Rect accessRect,
            out Rect routesRect)
        {
            routesRect = BuildRightBadgeRect(contentRect);
            var leftWidth = Mathf.Max(0f, routesRect.x - contentRect.x - VSigRowColumnGap);
            var nameWidth = leftWidth < VSigMinNameWidth
                ? leftWidth
                : Mathf.Clamp(leftWidth * 0.5f, VSigMinNameWidth, leftWidth);
            var remaining = Mathf.Max(0f, leftWidth - nameWidth - VSigRowColumnGap);
            var typeWidth = Mathf.Min(Mathf.Clamp(remaining * 0.62f, 48f, 120f), remaining);
            var accessWidth = Mathf.Max(0f, remaining - typeWidth - VSigRowColumnGap);
            if (accessWidth < VSigMinIoAccessWidth)
            {
                typeWidth = remaining;
                accessWidth = 0f;
            }

            nameRect = new Rect(contentRect.x, contentRect.y, nameWidth, contentRect.height);
            typeRect = new Rect(nameRect.xMax + VSigRowColumnGap, contentRect.y, typeWidth, contentRect.height);
            accessRect = new Rect(typeRect.xMax + VSigRowColumnGap, contentRect.y, accessWidth, contentRect.height);
        }

        private static void TryGetRouteDetails(EntityMarker marker,
            out Dictionary<string, List<string>> outputsBySignal,
            out Dictionary<string, List<string>> inputsBySignal, out string error)
        {
            outputsBySignal = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            inputsBySignal = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            error = null;

            if (!marker || !marker.gameObject)
            {
                error = "No marker selected.";
                return;
            }

            var scene = marker.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                error = "Save and load the scene to inspect VSig routes.";
                return;
            }

            var model = VSigRouteModelBuilder.Build(scene, true);
            if (!model.HasSource)
            {
                error = "No VSig file found for this scene.";
                return;
            }

            if (model.Program == null)
            {
                error = "Could not parse VSig.";
                return;
            }

            if (model.HasErrors)
            {
                error = BuildVsigErrorText(model.Diagnostics);
                return;
            }

            foreach (var route in model.Routes)
            {
                if (route.Source == marker)
                    AddRoute(outputsBySignal, route.OutputName, $"{route.TargetAlias}.{route.InputName}");

                if (route.Target == marker)
                    AddRoute(inputsBySignal, route.InputName, $"{route.SourceAlias}.{route.OutputName}");
            }
        }

        private static void AddRoute(Dictionary<string, List<string>> map, string signalName, string route)
        {
            var key = EntitySignalNameUtility.Normalize(signalName);
            if (!map.TryGetValue(key, out var routes))
            {
                routes = new List<string>();
                map[key] = routes;
            }

            if (!routes.Contains(route))
                routes.Add(route);
        }

        private static string BuildVsigErrorText(IReadOnlyList<VSigDiagnostic> diagnostics)
        {
            if (diagnostics == null)
                return "VSig diagnostics are unavailable.";

            foreach (var diagnostic in diagnostics)
                if (diagnostic is { Severity: VSigDiagnosticSeverity.Error })
                    return $"VSig error: {diagnostic.Message}";

            return "VSig diagnostics are unavailable.";
        }

        private void DrawAdvanced(EntityMarker marker)
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card))
            {
                _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true);
                if (_showAdvanced)
                {
                    EditorGUILayout.Space(4f);
                    SdkEditorGui.DrawInfoRow("Type", marker.GetType().FullName);
                    SdkEditorGui.DrawInfoRow("Instance ID", marker.GetInstanceID().ToString());
                    var script = MonoScript.FromMonoBehaviour(marker);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
                    }
                }
            }
        }

        private bool HasMarkerSpecificProperties()
        {
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (!IsBaseProperty(iterator.propertyPath))
                    return true;
            }

            return false;
        }

        private static bool IsBaseProperty(string propertyPath)
        {
            foreach (var baseProperty in BaseProperties)
                if (string.Equals(baseProperty, propertyPath, StringComparison.Ordinal))
                    return true;

            return false;
        }

        private static GUIStyle HeaderTitleStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                richText = true,
                normal = { textColor = SdkEditorStyle.PrimaryText }
            };
        }

        private static void OpenSceneVSig(EntityMarker marker)
        {
            if (!TryFindSceneVSig(marker, out var source, out var message))
            {
                EditorUtility.DisplayDialog("VSig", message, "OK");
                return;
            }

            AssetDatabase.OpenAsset(source);
        }

        private void AddSelectedRefsToSceneVSig(EntityMarker context)
        {
            var markers = CollectSelectedMarkers();
            if (markers.Count == 0)
                return;

            if (!TryFindOrCreateSceneVSig(context, out var path, out var source, out var message))
            {
                EditorUtility.DisplayDialog("VSig", message, "OK");
                return;
            }

            var refLines = new List<string>(markers.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var selectedMarker in markers)
            {
                var line = BuildRefLine(selectedMarker);
                if (!string.IsNullOrWhiteSpace(line) && seen.Add(line))
                    refLines.Add(line);
            }

            var text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var updated = InsertMissingRefs(text, refLines, out var added, out var conflicts);
            if (added > 0)
            {
                File.WriteAllText(path, updated);
                AssetDatabase.ImportAsset(path);
                source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (source)
                    AssetDatabase.OpenAsset(source);
            }
            else if (source)
            {
                AssetDatabase.OpenAsset(source);
            }

            if (conflicts > 0)
                SdkConsoleBridge.Warn(
                    $"VSig left {conflicts} conflicting alias(es) unchanged while updating '{path}'.");
        }

        private void AddSelectedGroupToSceneVSig(EntityMarker context)
        {
            var markers = CollectSelectedMarkers();
            if (markers.Count < 2)
                return;

            if (!TryFindOrCreateSceneVSig(context, out var path, out var source, out var message))
            {
                EditorUtility.DisplayDialog("VSig", message, "OK");
                return;
            }

            var refLines = new List<string>(markers.Count);
            var memberAliases = new List<string>(markers.Count);
            var seenRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var marker in markers)
            {
                var refLine = BuildRefLine(marker);
                if (!string.IsNullOrWhiteSpace(refLine) && seenRefs.Add(refLine))
                    refLines.Add(refLine);

                var alias = VSigSceneCompiler.BuildAlias(marker);
                if (!string.IsNullOrWhiteSpace(alias) && seenMembers.Add(alias))
                    memberAliases.Add(alias);
            }

            if (memberAliases.Count == 0)
                return;

            var text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            text = InsertMissingRefs(text, refLines, out _, out _);
            var groupAlias = BuildAvailableGroupAlias(text, markers);
            var updated = InsertGroup(text, groupAlias, memberAliases);

            File.WriteAllText(path, updated);
            AssetDatabase.ImportAsset(path);
            source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (source)
                AssetDatabase.OpenAsset(source);
        }

        private static void CopyRef(EntityMarker marker)
        {
            var line = BuildRefLine(marker);
            if (!string.IsNullOrWhiteSpace(line))
                EditorGUIUtility.systemCopyBuffer = line;
        }

        private string DescribeSelectedEntityClasses()
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var targetObject in targets)
            {
                if (targetObject is not EntityMarker marker || !marker)
                    continue;

                var entityClass = marker.EntityClassName;
                if (!string.IsNullOrWhiteSpace(entityClass))
                    values.Add(entityClass);
            }

            return DescribeValues(values);
        }

        private static string DescribeValues(ICollection<string> values)
        {
            if (values == null || values.Count == 0)
                return "None";

            if (values.Count == 1)
                foreach (var value in values)
                    return value;

            return "Mixed";
        }

        private static bool TryFindSceneVSig(EntityMarker marker, out TextAsset source, out string message)
        {
            source = null;
            message = null;

            if (!marker || !marker.gameObject)
            {
                message = "No marker is selected.";
                return false;
            }

            var scene = marker.gameObject.scene;
            if (string.IsNullOrWhiteSpace(scene.path))
            {
                message = "Save the scene before opening its VSig file.";
                return false;
            }

            var status = VSigMapSourceUtility.FindForScene(scene.path, out source, out _, out var expectedPath);
            switch (status)
            {
                case VSigMapSourceStatus.Found:
                    return true;
                case VSigMapSourceStatus.Multiple:
                    message =
                        $"Multiple VSig files were found in '{Path.GetDirectoryName(expectedPath)}'. Expected '{expectedPath}'.";
                    return false;
                case VSigMapSourceStatus.InvalidScenePath:
                    message = $"Could not resolve a VSig folder for scene '{scene.name}'.";
                    return false;
                default:
                    message = $"No VSig file was found for scene '{scene.name}'. Expected '{expectedPath}'.";
                    return false;
            }
        }

        private static bool TryFindOrCreateSceneVSig(EntityMarker marker, out string sourcePath, out TextAsset source,
            out string message)
        {
            sourcePath = null;
            source = null;
            message = null;

            if (!TryGetSceneVSigPath(marker, out var expectedPath, out message))
                return false;

            var scenePath = marker.gameObject.scene.path;
            var status = VSigMapSourceUtility.FindForScene(scenePath, out source, out sourcePath, out _);
            switch (status)
            {
                case VSigMapSourceStatus.Found:
                    return true;
                case VSigMapSourceStatus.Multiple:
                    message =
                        $"Multiple VSig files were found in '{Path.GetDirectoryName(expectedPath)}'. Expected '{expectedPath}'.";
                    return false;
                case VSigMapSourceStatus.InvalidScenePath:
                    message = $"Could not resolve a VSig folder for scene '{marker.gameObject.scene.name}'.";
                    return false;
            }

            return VSigMapSourceUtility.TryEnsureForScene(scenePath, out source, out sourcePath, out _, out message);
        }

        private static bool TryGetSceneVSigPath(EntityMarker marker, out string path, out string message)
        {
            path = null;
            message = null;

            if (!marker || !marker.gameObject)
            {
                message = "No marker is selected.";
                return false;
            }

            var scene = marker.gameObject.scene;
            if (string.IsNullOrWhiteSpace(scene.path))
            {
                message = "Save the scene before editing its VSig file.";
                return false;
            }

            if (!VSigMapSourceUtility.TryGetExpectedPathForScene(scene.path, out path))
            {
                message = $"Could not resolve a VSig folder for scene '{scene.name}'.";
                return false;
            }

            return true;
        }

        private List<EntityMarker> CollectSelectedMarkers()
        {
            var markers = new List<EntityMarker>();
            var seen = new HashSet<int>();
            foreach (var targetObject in targets)
            {
                if (targetObject is not EntityMarker marker || !marker)
                    continue;

                if (seen.Add(marker.GetInstanceID()))
                    markers.Add(marker);
            }

            return markers;
        }

        private static string BuildRefLine(EntityMarker marker)
        {
            if (!marker)
                return null;

            var alias = VSigSceneCompiler.BuildAlias(marker);
            var entityClass = marker.EntityClassName;
            return string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(entityClass)
                ? null
                : $"ref {alias}: {entityClass}";
        }

        private static string InsertMissingRefs(string source, IReadOnlyList<string> refLines, out int added,
            out int conflicts)
        {
            added = 0;
            conflicts = 0;
            source ??= string.Empty;
            var lines = new List<string>(source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
            if (lines.Count == 1 && string.IsNullOrWhiteSpace(lines[0]))
                lines.Clear();

            var existingRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sourceLine in lines)
            {
                var trimmed = StripLineComment(sourceLine).Trim();
                if (!trimmed.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
                    continue;

                existingRefs.Add(trimmed);
                var alias = ParseRefAlias(trimmed);
                if (!string.IsNullOrWhiteSpace(alias))
                    existingAliases.TryAdd(alias, trimmed);
            }

            var missing = new List<string>();
            foreach (var refLine in refLines)
            {
                var line = refLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || existingRefs.Contains(line))
                    continue;

                var alias = ParseRefAlias(line);
                if (!string.IsNullOrWhiteSpace(alias) && existingAliases.TryGetValue(alias, out var existing) &&
                    !string.Equals(existing, line, StringComparison.OrdinalIgnoreCase))
                {
                    conflicts++;
                    continue;
                }

                missing.Add(line);
                existingRefs.Add(line);
                if (!string.IsNullOrWhiteSpace(alias))
                    existingAliases[alias] = line;
            }

            if (missing.Count == 0)
                return NormalizeLines(lines);

            var insertion = FindRefInsertionIndex(lines);
            lines.InsertRange(insertion, missing);
            added = missing.Count;

            if (lines.Count > insertion + missing.Count &&
                !string.IsNullOrWhiteSpace(lines[insertion + missing.Count]))
                lines.Insert(insertion + missing.Count, string.Empty);

            return NormalizeLines(lines);
        }

        private static string InsertGroup(string source, string groupAlias, IReadOnlyList<string> memberAliases)
        {
            source ??= string.Empty;
            var lines = new List<string>(source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
            if (lines.Count == 1 && string.IsNullOrWhiteSpace(lines[0]))
                lines.Clear();

            var groupLines = new List<string> { $"group {groupAlias}:" };
            foreach (var memberAlias in memberAliases)
                groupLines.Add($"    {memberAlias}");

            var insertion = FindGroupInsertionIndex(lines);
            if (insertion > 0 && insertion <= lines.Count && !string.IsNullOrWhiteSpace(lines[insertion - 1]))
                groupLines.Insert(0, string.Empty);
            if (insertion < lines.Count && !string.IsNullOrWhiteSpace(lines[insertion]))
                groupLines.Add(string.Empty);

            lines.InsertRange(insertion, groupLines);
            return NormalizeLines(lines);
        }

        private static string BuildAvailableGroupAlias(string source, IReadOnlyList<EntityMarker> markers)
        {
            var baseAlias = markers.Count == 1 ? VSigSceneCompiler.BuildAlias(markers[0]) : "selection";
            if (string.IsNullOrWhiteSpace(baseAlias))
                baseAlias = "selection";
            baseAlias = $"{baseAlias}_group";

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = source?.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n') ?? Array.Empty<string>();
            foreach (var line in lines)
            {
                var trimmed = StripLineComment(line).Trim();
                var refAlias = ParseRefAlias(trimmed);
                if (!string.IsNullOrWhiteSpace(refAlias))
                    used.Add(refAlias);

                if (trimmed.StartsWith("group ", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = trimmed["group ".Length..].Trim();
                    var colon = rest.IndexOf(':');
                    if (colon >= 0)
                        rest = rest[..colon].Trim();
                    if (!string.IsNullOrWhiteSpace(rest))
                        used.Add(rest);
                }
            }

            var alias = baseAlias;
            var suffix = 2;
            while (!used.Add(alias))
                alias = $"{baseAlias}_{suffix++}";
            return alias;
        }

        private static int FindRefInsertionIndex(IReadOnlyList<string> lines)
        {
            var insertion = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                var trimmed = StripLineComment(lines[i]).Trim();
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith("scene ", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
                {
                    insertion = i + 1;
                    continue;
                }

                break;
            }

            return insertion;
        }

        private static int FindGroupInsertionIndex(IReadOnlyList<string> lines)
        {
            var insertion = FindRefInsertionIndex(lines);
            for (var i = insertion; i < lines.Count; i++)
            {
                var trimmed = StripLineComment(lines[i]).Trim();
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith("group ", StringComparison.OrdinalIgnoreCase) ||
                    IsIndentedGroupMember(lines[i]))
                {
                    insertion = i + 1;
                    continue;
                }

                break;
            }

            return insertion;
        }

        private static bool IsIndentedGroupMember(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !char.IsWhiteSpace(line[0]))
                return false;

            var trimmed = StripLineComment(line).Trim();
            return !string.IsNullOrWhiteSpace(trimmed) &&
                   trimmed.IndexOf('.') < 0 &&
                   trimmed.IndexOf('=') < 0 &&
                   !trimmed.StartsWith("wait ", StringComparison.OrdinalIgnoreCase);
        }

        private static string ParseRefAlias(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
                return null;

            var rest = line["ref ".Length..].Trim();
            var end = rest.IndexOfAny(new[] { ' ', '\t', ':' });
            return end < 0 ? rest : rest[..end].Trim();
        }

        private static string StripLineComment(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            var inString = false;
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
                    inString = !inString;
                if (!inString && line[i] == '#')
                    return line[..i];
                if (!inString && line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    return line[..i];
            }

            return line;
        }

        private static string NormalizeLines(IReadOnlyList<string> lines)
        {
            if (lines == null || lines.Count == 0)
                return string.Empty;

            var end = lines.Count;
            while (end > 0 && string.IsNullOrWhiteSpace(lines[end - 1]))
                end--;

            if (end == 0)
                return string.Empty;

            var kept = new List<string>(end);
            var remaining = end;
            foreach (var sourceLine in lines)
            {
                if (remaining-- <= 0)
                    break;

                kept.Add(sourceLine);
            }

            return string.Join("\n", kept) + "\n";
        }

        private static void DuplicateMarker(EntityMarker marker)
        {
            if (!marker) return;
            var go = marker.gameObject;
            if (!go) return;

            var clone = Instantiate(go, go.transform.parent);
            GameObjectUtility.EnsureUniqueNameForSibling(clone);
            clone.transform.position += new Vector3(0.25f, 0f, 0.25f);

            Undo.RegisterCreatedObjectUndo(clone, "Duplicate Marker");
            Selection.activeObject = clone;
        }

        private void SnapMarkersToGround()
        {
            var transforms = new Transform[targets.Length];
            var count = 0;
            foreach (var targetObject in targets)
                if (targetObject is EntityMarker marker && marker && marker.transform)
                    transforms[count++] = marker.transform;

            if (count == 0)
                return;

            Array.Resize(ref transforms, count);
            var objects = new Object[transforms.Length];
            for (var i = 0; i < transforms.Length; i++)
                objects[i] = transforms[i];
            Undo.RecordObjects(objects, "Snap Markers To Ground");

            foreach (var t in transforms)
            {
                if (!TryGetGroundPoint(t, out var hitPoint))
                    continue;

                t.position = hitPoint;
                EditorUtility.SetDirty(t);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }
        }

        private static bool TryGetGroundPoint(Transform t, out Vector3 hitPoint)
        {
            const float snapRayHeight = 10000f;
            const float snapRayDistance = 20000f;

            hitPoint = t ? t.position : Vector3.zero;
            if (!t) return false;

            var origin = t.position + Vector3.up * snapRayHeight;
            var ray = new Ray(origin, Vector3.down);
            var hitCount = Physics.RaycastNonAlloc(ray, SnapRaycastHits, snapRayDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0) return false;

            var targetY = t.position.y + 0.01f;
            var found = false;
            var bestY = float.NegativeInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                var hit = SnapRaycastHits[i];
                var col = hit.collider;
                if (!col) continue;
                var hitTransform = col.transform;
                if (hitTransform == t || hitTransform.IsChildOf(t)) continue;
                if (hit.point.y > targetY) continue;
                if (hit.point.y <= bestY) continue;
                bestY = hit.point.y;
                hitPoint = hit.point;
                found = true;
            }

            return found;
        }
    }
}
