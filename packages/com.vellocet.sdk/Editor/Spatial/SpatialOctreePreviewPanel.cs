// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Spatial;

namespace Vellocet.Sdk.Editor.Spatial
{
    public sealed class SpatialOctreePreviewPanel
    {
        private const int MaxDrawnNodes = 8000;
        private static readonly Color FreeColor = new(0.1f, 0.9f, 0.45f, 0.22f);
        private static readonly Color BlockedColor = new(1f, 0.25f, 0.15f, 0.18f);
        private static readonly Color EdgeColor = new(0.2f, 0.65f, 1f, 0.18f);
        private static readonly Color PathColor = new(1f, 0.9f, 0.1f, 0.95f);
        private readonly List<Vector3> _path = new(64);

        private SpatialOctreeAsset _asset;
        private bool _drawBlockedLeaves;
        private bool _drawEdges = true;
        private bool _drawFreeLeaves = true;
        private bool _drawPath = true;
        private SpatialOctreeGraph _graph;
        private EditorWindow _host;
        private int _maxDepth = 8;
        private Vector3 _pathEnd;
        private Vector3 _pathStart;
        private string _pathStatus = "No path sampled.";
        private bool _subscribed;

        public void OnEnable(EditorWindow host)
        {
            _host = host;
            if (_subscribed)
                return;

            _subscribed = true;
            SceneView.duringSceneGui += DuringSceneGui;
            Selection.selectionChanged += OnSelectionChanged;
            TryAdoptSelection();
        }

        public void OnDisable()
        {
            if (_subscribed)
            {
                SceneView.duringSceneGui -= DuringSceneGui;
                Selection.selectionChanged -= OnSelectionChanged;
                _subscribed = false;
            }

            _graph?.Dispose();
            _graph = null;
            _host = null;
        }

        public void DrawPreviewWorkspace(float minHeight = 260f)
        {
            using (new EditorGUILayout.VerticalScope(SdkEditorGui.Card, GUILayout.ExpandHeight(true)))
            {
                SdkEditorGui.DrawSectionHeader("Spatial octree",
                    "View a baked graph and test paths in the Scene view.");
                EditorGUILayout.Space(6f);
                DrawControls();
            }

            GUILayout.Space(Mathf.Max(0f, minHeight - 260f));
        }

        private void DrawControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _asset = (SpatialOctreeAsset)EditorGUILayout.ObjectField("Asset", _asset, typeof(SpatialOctreeAsset),
                    false);
                if (EditorGUI.EndChangeCheck())
                    RebuildGraph();

                using (new EditorGUI.DisabledScope(!_asset))
                {
                    if (GUILayout.Button("Ping", SdkEditorGui.SecondaryButton,
                            GUILayout.Width(62f), GUILayout.Height(22f)))
                        EditorGUIUtility.PingObject(_asset);
                }
            }

            using (new EditorGUI.DisabledScope(!_asset))
            {
                _drawFreeLeaves = EditorGUILayout.Toggle("Free leaves", _drawFreeLeaves);
                _drawBlockedLeaves = EditorGUILayout.Toggle("Blocked leaves", _drawBlockedLeaves);
                _drawEdges = EditorGUILayout.Toggle("Graph edges", _drawEdges);
                _drawPath = EditorGUILayout.Toggle("Path probe", _drawPath);
                _maxDepth = EditorGUILayout.IntSlider("Max depth", _maxDepth, 1, 14);
                _pathStart = EditorGUILayout.Vector3Field("Path start", _pathStart);
                _pathEnd = EditorGUILayout.Vector3Field("Path end", _pathEnd);

                if (GUILayout.Button("Sample path", SdkEditorGui.PrimaryButton, GUILayout.Height(28f)))
                    SamplePath();

                SdkEditorGui.DrawCallout(_pathStatus);
            }
        }

        private void OnSelectionChanged()
        {
            TryAdoptSelection();
            _host?.Repaint();
            SceneView.RepaintAll();
        }

        private void TryAdoptSelection()
        {
            var selectedAsset = Selection.activeObject as SpatialOctreeAsset;
            if (selectedAsset) SetAsset(selectedAsset);
        }

        private void SetAsset(SpatialOctreeAsset asset)
        {
            if (_asset == asset)
                return;

            _asset = asset;
            RebuildGraph();
        }

        private void RebuildGraph()
        {
            _graph?.Dispose();
            _graph = _asset && _asset.IsUsable ? new SpatialOctreeGraph(_asset) : null;
            _path.Clear();
            if (_asset)
            {
                var bounds = _asset.Bounds;
                _pathStart = bounds.center + Vector3.left * bounds.extents.x * 0.5f;
                _pathEnd = bounds.center + Vector3.right * bounds.extents.x * 0.5f;
                _pathStatus = $"{_asset.Nodes.Length} nodes; {_asset.TraversableLeafCount} traversable leaves.";
            }
            else
            {
                _pathStatus = "No asset selected.";
            }
        }

        private void SamplePath()
        {
            _path.Clear();
            if (_graph == null || !_graph.IsUsable)
            {
                _pathStatus = "No usable graph.";
                return;
            }

            if (!_graph.TryFindPath(_pathStart, _pathEnd, _path))
            {
                _pathStatus = "No path found.";
                return;
            }

            var length = 0f;
            for (var i = 1; i < _path.Count; i++)
                length += Vector3.Distance(_path[i - 1], _path[i]);

            _pathStatus = $"Path: {_path.Count} waypoints, {length:0.##} m.";
            SceneView.RepaintAll();
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            if (!_asset || !_asset.IsUsable)
                return;

            DrawNodes();
            if (_drawEdges)
                DrawEdges();
            if (_drawPath)
                DrawPathProbe();
        }

        private void DrawNodes()
        {
            var drawn = 0;
            foreach (var node in _asset.Nodes)
            {
                if (!node.IsLeaf || node.Depth > _maxDepth)
                    continue;
                if (node.IsBlocked && !_drawBlockedLeaves)
                    continue;
                if (!node.IsBlocked && !_drawFreeLeaves)
                    continue;
                if (++drawn > MaxDrawnNodes)
                    break;

                Handles.color = node.IsBlocked ? BlockedColor : FreeColor;
                Handles.DrawWireCube(node.Bounds.center, node.Bounds.size);
            }
        }

        private void DrawEdges()
        {
            var nodes = _asset.Nodes;
            var starts = _asset.NeighborStarts;
            var indices = _asset.NeighborIndices;
            Handles.color = EdgeColor;

            for (var i = 0; i < nodes.Length; i++)
            {
                if (!nodes[i].IsTraversable || nodes[i].Depth > _maxDepth)
                    continue;

                var start = starts[i];
                var end = starts[i + 1];
                for (var n = start; n < end; n++)
                {
                    var neighbor = indices[n];
                    if (neighbor <= i || (uint)neighbor >= (uint)nodes.Length || nodes[neighbor].Depth > _maxDepth)
                        continue;

                    Handles.DrawLine(nodes[i].Bounds.center, nodes[neighbor].Bounds.center);
                }
            }
        }

        private void DrawPathProbe()
        {
            EditorGUI.BeginChangeCheck();
            _pathStart = Handles.PositionHandle(_pathStart, Quaternion.identity);
            _pathEnd = Handles.PositionHandle(_pathEnd, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
                SamplePath();

            Handles.color = PathColor;
            Handles.SphereHandleCap(0, _pathStart, Quaternion.identity, HandleUtility.GetHandleSize(_pathStart) * 0.12f,
                EventType.Repaint);
            Handles.SphereHandleCap(0, _pathEnd, Quaternion.identity, HandleUtility.GetHandleSize(_pathEnd) * 0.12f,
                EventType.Repaint);

            if (_path.Count < 2)
                return;

            Handles.DrawAAPolyLine(5f, _path.ToArray());
        }
    }
}