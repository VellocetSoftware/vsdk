// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using Vertex.VSig;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.VSig;

namespace Vellocet.Sdk.Editor.VSig
{
    [InitializeOnLoad]
    internal static class VSigSceneRouteGizmos
    {
        private const string ShowRoutesKey = "Vellocet.Sdk.VSig.ShowSceneRoutes";
        private const string MenuPath = "Tools/Vellocet/SDK/VSig/Show Scene Routes";
        private const float ViewportPadding = 0.08f;
        private static readonly Color OutgoingColor = new(0.18f, 0.78f, 1f, 0.92f);
        private static readonly Color IncomingColor = new(0.95f, 0.58f, 0.18f, 0.92f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.34f);

        static VSigSceneRouteGizmos()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static bool ShowRoutesEnabled => EditorPrefs.GetBool(ShowRoutesKey, true);

        [MenuItem(MenuPath, false, 506)]
        private static void ToggleShowRoutes()
        {
            EditorPrefs.SetBool(ShowRoutesKey, !ShowRoutesEnabled);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleShowRoutesValidate()
        {
            Menu.SetChecked(MenuPath, ShowRoutesEnabled);
            return true;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!sceneView || !sceneView.drawGizmos || !ShowRoutesEnabled || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
                return;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            var model = VSigRouteModelCache.Get(scene);
            if (!model.HasSource)
                return;

            var selection = GetSelectedMarkers(model);
            if (selection.Expanded.Count == 0)
                return;

            if (model.HasErrors)
            {
                DrawDiagnosticBanner(model.Diagnostics);
                return;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            DrawRoutes(model.Routes, selection, sceneView.camera);
        }

        private static SelectionContext GetSelectedMarkers(VSigRouteModel model)
        {
            var expanded = new HashSet<int>();
            var direct = new HashSet<int>();
            var gameObjects = Selection.gameObjects;
            for (var i = 0; i < gameObjects.Length; i++)
            {
                var gameObject = gameObjects[i];
                if (!gameObject)
                    continue;

                var marker = gameObject.GetComponentInParent<EntityMarker>();
                if (marker)
                {
                    var markerId = marker.GetInstanceID();
                    direct.Add(markerId);
                    expanded.Add(markerId);
                    continue;
                }

                for (var transform = gameObject.transform; transform; transform = transform.parent)
                    if (model.MarkerIdsByModuleRootId.TryGetValue(transform.GetInstanceID(), out var ids))
                        for (var idIndex = 0; idIndex < ids.Length; idIndex++)
                            expanded.Add(ids[idIndex]);
            }

            return new SelectionContext(expanded, direct);
        }

        private static void DrawRoutes(IReadOnlyList<VSigRouteModel.Route> modelRoutes, SelectionContext selection,
            Camera sceneCamera)
        {
            var routes = new List<RouteDrawData>();
            var countsByPair = new Dictionary<RoutePairKey, int>();
            for (var routeIndex = 0; routeIndex < modelRoutes.Count; routeIndex++)
            {
                var modelRoute = modelRoutes[routeIndex];
                var source = modelRoute.Source;
                var target = modelRoute.Target;
                if (!source || !target)
                    continue;

                var sourceId = source.GetInstanceID();
                var targetId = target.GetInstanceID();
                var sourceSelected = selection.Expanded.Contains(sourceId);
                var targetSelected = selection.Expanded.Contains(targetId);
                if (!sourceSelected && !targetSelected)
                    continue;

                var sourceDirect = selection.Direct.Contains(sourceId);
                var targetDirect = selection.Direct.Contains(targetId);
                var incoming = targetDirect && !sourceDirect;
                var pair = new RoutePairKey(source, target);
                countsByPair.TryGetValue(pair, out var count);
                countsByPair[pair] = count + 1;
                routes.Add(new RouteDrawData(source, target, incoming, pair));
            }

            var drawnByPair = new Dictionary<RoutePairKey, int>();
            for (var i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                drawnByPair.TryGetValue(route.PairKey, out var index);
                drawnByPair[route.PairKey] = index + 1;
                var count = countsByPair.TryGetValue(route.PairKey, out var pairCount) ? pairCount : 1;
                var geometry = BuildRouteGeometry(route.Source.transform.position, route.Target.transform.position,
                    index,
                    count);
                if (!IsRouteVisible(sceneCamera, geometry))
                    continue;

                DrawWire(geometry, route.Incoming);
            }
        }

        private static void DrawWire(RouteGeometry geometry, bool incoming)
        {
            var color = incoming ? IncomingColor : OutgoingColor;
            var width = 3f;
            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            try
            {
                using (new Handles.DrawingScope(ShadowColor))
                {
                    Handles.DrawBezier(geometry.P0, geometry.P3, geometry.P1, geometry.P2, ShadowColor, null,
                        width + 3f);
                }

                using (new Handles.DrawingScope(color))
                {
                    Handles.DrawBezier(geometry.P0, geometry.P3, geometry.P1, geometry.P2, color, null, width);
                }
            }
            finally
            {
                Handles.zTest = previousZTest;
            }
        }

        private static RouteGeometry BuildRouteGeometry(Vector3 from, Vector3 to, int stackIndex, int stackCount)
        {
            from += Vector3.up * 0.35f;
            to += Vector3.up * 0.35f;

            var delta = to - from;
            var side = Vector3.Cross(delta.sqrMagnitude > 0f ? delta.normalized : Vector3.forward, Vector3.up);
            if (side.sqrMagnitude < 0.000001f)
                side = Vector3.right;
            side.Normalize();

            var lane = stackIndex - (stackCount - 1) * 0.5f;
            var curveSeparation = stackCount > 1 ? Mathf.Clamp(delta.magnitude * 0.055f, 0.18f, 0.75f) : 0f;
            var bend = side * (Mathf.Min(2.5f, delta.magnitude * 0.22f) + lane * curveSeparation);
            return new RouteGeometry(from, from + bend, to - bend, to);
        }

        private static bool IsRouteVisible(Camera camera, RouteGeometry geometry)
        {
            if (!camera)
                return true;

            return IsPointVisible(camera, geometry.P0) ||
                   IsPointVisible(camera, geometry.P3) ||
                   IsPointVisible(camera, Evaluate(geometry.P0, geometry.P1, geometry.P2, geometry.P3, 0.5f));
        }

        private static bool IsPointVisible(Camera camera, Vector3 worldPosition)
        {
            var viewport = camera.WorldToViewportPoint(worldPosition);
            return viewport.z > camera.nearClipPlane &&
                   viewport.x >= -ViewportPadding &&
                   viewport.x <= 1f + ViewportPadding &&
                   viewport.y >= -ViewportPadding &&
                   viewport.y <= 1f + ViewportPadding;
        }

        private static Vector3 Evaluate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        private static void DrawDiagnosticBanner(IEnumerable<VSigDiagnostic> diagnostics)
        {
            var errors = CollectDiagnosticErrors(diagnostics);
            if (errors.Count == 0)
                return;

            var summary = errors.Count == 1
                ? $"VSig: {errors[0]}"
                : $"VSig: {errors[0]}\n(+{errors.Count - 1} more error{(errors.Count == 2 ? string.Empty : "s")})";
            var copyAll = string.Join("\n", errors);

            Handles.BeginGUI();
            var hasMultipleErrors = errors.Count > 1;
            var copyButtonWidth = 56f;
            var copyAllButtonWidth = hasMultipleErrors ? 78f : 0f;
            var buttonSpacing = hasMultipleErrors ? 6f : 0f;
            var panelWidth = Mathf.Min(820f, Screen.width - 24f);
            var panelHeight = hasMultipleErrors ? 52f : 34f;
            var rect = new Rect(12f, 12f, panelWidth, panelHeight);
            EditorGUI.DrawRect(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), ShadowColor);
            EditorGUI.DrawRect(rect, new Color(0.25f, 0.08f, 0.06f, 0.92f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), new Color(1f, 0.34f, 0.18f, 1f));

            var rightPadding = 10f;
            var buttonY = rect.y + (rect.height - 20f) * 0.5f;
            var copyRect = new Rect(rect.xMax - rightPadding - copyButtonWidth, buttonY, copyButtonWidth, 20f);
            var copyAllRect = new Rect(copyRect.x - buttonSpacing - copyAllButtonWidth, buttonY, copyAllButtonWidth,
                20f);
            var labelRight = hasMultipleErrors ? copyAllRect.x : copyRect.x;
            var labelRect = new Rect(rect.x + 10f, rect.y + 7f, labelRight - rect.x - 18f, rect.height - 10f);
            GUI.Label(labelRect, summary, EditorStyles.whiteMiniLabel);

            if (hasMultipleErrors && GUI.Button(copyAllRect, "Copy All"))
                GUIUtility.systemCopyBuffer = copyAll;

            if (GUI.Button(copyRect, "Copy"))
                GUIUtility.systemCopyBuffer = errors[0];

            Handles.EndGUI();
        }

        private static List<string> CollectDiagnosticErrors(IEnumerable<VSigDiagnostic> diagnostics)
        {
            var errors = new List<string>();
            if (diagnostics == null)
                return errors;

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic == null || diagnostic.Severity != VSigDiagnosticSeverity.Error)
                    continue;

                errors.Add(diagnostic.ToString());
            }

            return errors;
        }

        private readonly struct RouteDrawData
        {
            public RouteDrawData(EntityMarker source, EntityMarker target, bool incoming, RoutePairKey pairKey)
            {
                Source = source;
                Target = target;
                Incoming = incoming;
                PairKey = pairKey;
            }

            public EntityMarker Source { get; }
            public EntityMarker Target { get; }
            public bool Incoming { get; }
            public RoutePairKey PairKey { get; }
        }

        private readonly struct SelectionContext
        {
            public SelectionContext(HashSet<int> expanded, HashSet<int> direct)
            {
                Expanded = expanded ?? new HashSet<int>();
                Direct = direct ?? new HashSet<int>();
            }

            public HashSet<int> Expanded { get; }
            public HashSet<int> Direct { get; }
        }

        private readonly struct RouteGeometry
        {
            public RouteGeometry(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                P0 = p0;
                P1 = p1;
                P2 = p2;
                P3 = p3;
            }

            public Vector3 P0 { get; }
            public Vector3 P1 { get; }
            public Vector3 P2 { get; }
            public Vector3 P3 { get; }
        }

        private readonly struct RoutePairKey : IEquatable<RoutePairKey>
        {
            public RoutePairKey(EntityMarker a, EntityMarker b)
            {
                var aId = a ? a.GetInstanceID() : 0;
                var bId = b ? b.GetInstanceID() : 0;
                if (aId <= bId)
                {
                    First = aId;
                    Second = bId;
                }
                else
                {
                    First = bId;
                    Second = aId;
                }
            }

            private int First { get; }
            private int Second { get; }

            public bool Equals(RoutePairKey other)
            {
                return First == other.First && Second == other.Second;
            }

            public override bool Equals(object obj)
            {
                return obj is RoutePairKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (First * 397) ^ Second;
                }
            }
        }

    }
}
#endif
