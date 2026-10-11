// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Vellocet.Sdk.Entities.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities
{
    [EntityClass("game_blitz_tank")]
    [EntityAuthoring("Objectives", EntityPlacementKind.Volume, "Blitz Tank")]
    public sealed class BlitzTankMarker : TriggerVolumeMarkerBase<BlitzTankMarker.Input, BlitzTankMarker.Output>, ISplineEntityMarker
    {
        public enum Input
        {
            Lock,
            Unlock,
            Start,
            Stop,
            Reset,
            SetProgress
        }

        public enum Output
        {
            OnStarted,
            OnStopped,
            OnContested,
            OnUncontested,
            OnCheckpoint,
            OnFinalCheckpoint,
            OnReset,
            OnProgress
        }

        private const string RuntimeTankPrefabPath = "Assets/GameContent/Prefabs/Entities/SDK/Internal/Tank.prefab";
        private const string RuntimeTriggerChildName = "Trigger";

        [field: Header("Display")]
        [field: Tooltip("Display name for this tank objective.")]
        [field: SerializeField]
        public string TankName { get; private set; } = "Tank";

        [field: Header("Teams")]
        [field: Tooltip("Team that must escort this tank.")]
        [field: SerializeField]
        public Team AttackingTeam { get; private set; } = Team.Order;

        [field: Header("Round")]
        [field: Tooltip("Starting tank progress for each round.")]
        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float StartProgress01 { get; private set; }

        [field: Header("Path")]
        [field: Tooltip("Spline container that defines the tank's movement path.")]
        [field: SerializeField]
        public SplineContainer SplineContainer { get; private set; }

        protected override bool DefaultIgnoreFromNavMeshBuild => true;
        protected override Color GizmoFillColor => new(0.97f, 0.62f, 0.18f, 0.12f);
        protected override Color GizmoWireColor => new(0.97f, 0.62f, 0.18f, 0.9f);
        protected override Vector3 InitialAuthoringSize => ResolveVolumeSize();

        public void CollectCheckpointChildren(List<BlitzCheckpointMarker> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child || !child.TryGetComponent<BlitzCheckpointMarker>(out var checkpoint) || !checkpoint)
                    continue;

                results.Add(checkpoint);
            }
        }

#if UNITY_EDITOR
        private static bool TryGetRuntimePrefabVolumeSize(out Vector3 volumeSize)
        {
            volumeSize = default;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeTankPrefabPath);
            if (!prefab)
                return false;

            BoxCollider sourceBox = null;
            var trigger = prefab.transform.Find(RuntimeTriggerChildName);
            if (trigger)
                sourceBox = trigger.GetComponent<BoxCollider>();
            if (!sourceBox)
                sourceBox = prefab.GetComponentInChildren<BoxCollider>(true);
            if (!sourceBox)
                return false;

            var size = sourceBox.size;
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
                return false;

            volumeSize = size;
            return true;
        }
#endif

        private static Vector3 ResolveVolumeSize()
        {
            var size = new Vector3(8f, 2f, 12f);
#if UNITY_EDITOR
            if (TryGetRuntimePrefabVolumeSize(out var prefabSize))
                size = prefabSize;
#endif
            return size;
        }

#if UNITY_EDITOR
        private const int SplineSampleCount = 256;
        private const int SplineGizmoSegments = 96;
        private const float SplineGizmoWidth = 6f;
        private static readonly Color SplineGizmoColor = new(0.16f, 0.92f, 0.28f, 0.95f);
        private static readonly HashSet<int> PendingAuthoringSync = new();

        protected override void OnValidate()
        {
            base.OnValidate();
            if (Application.isPlaying)
                return;

            QueueAuthoringSync();
        }

        private void QueueAuthoringSync()
        {
            var instanceId = GetInstanceID();
            if (!PendingAuthoringSync.Add(instanceId))
                return;

            var marker = this;
            EditorApplication.delayCall += () =>
            {
                PendingAuthoringSync.Remove(instanceId);
                if (!marker || Application.isPlaying)
                    return;

                marker.RunAuthoringSync();
            };
        }

        internal void RequestAuthoringSyncFromCheckpoint()
        {
            if (!Application.isPlaying)
                QueueAuthoringSync();
        }

        private void OnDrawGizmos()
        {
            var spline = SplineContainer;
            if (!TryEvaluateSplinePosition(spline, 0f, out var previous))
                return;

            var cachedHandlesColor = Handles.color;
            Handles.color = SplineGizmoColor;

            for (var i = 1; i <= SplineGizmoSegments; i++)
            {
                var t = i / (float)SplineGizmoSegments;
                if (!TryEvaluateSplinePosition(spline, t, out var current))
                    break;

                Handles.DrawAAPolyLine(SplineGizmoWidth, previous, current);
                previous = current;
            }

            Handles.color = cachedHandlesColor;
        }

        private void RunAuthoringSync()
        {
            var changed = false;
            var spline = SplineContainer;
            if (spline)
                changed |= EnsureTankAtSplineStart(spline);

            var checkpoints = CollectAuthoringCheckpoints();
            changed |= AutoBindCheckpointTanks(checkpoints);
            changed |= EnsureCheckpointParenting(checkpoints);
            if (spline)
                changed |= SnapCheckpointsToSpline(spline, checkpoints);
            changed |= EnsureCheckpointOrdering(checkpoints, spline);

            if (!changed)
                return;

            EditorUtility.SetDirty(this);
            if (!PrefabUtility.IsPartOfPrefabAsset(this))
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }

        private bool EnsureTankAtSplineStart(SplineContainer spline)
        {
            if (!TryEvaluateSplinePosition(spline, 0f, out var splineStart))
                return false;

            if ((splineStart - transform.position).sqrMagnitude <= 0.000001f)
                return false;

            var childSnapshots = new List<(Transform child, Vector3 worldPosition)>(transform.childCount);
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child)
                    childSnapshots.Add((child, child.position));
            }

            Undo.RecordObject(transform, "Align Blitz Tank To Spline Start");
            transform.position = splineStart;
            EditorUtility.SetDirty(transform);

            for (var i = 0; i < childSnapshots.Count; i++)
            {
                var snapshot = childSnapshots[i];
                if (!snapshot.child)
                    continue;

                Undo.RecordObject(snapshot.child, "Preserve Child Position While Aligning Tank");
                snapshot.child.position = snapshot.worldPosition;
                EditorUtility.SetDirty(snapshot.child);
            }

            return true;
        }

        private bool AutoBindCheckpointTanks(IReadOnlyList<BlitzCheckpointMarker> checkpoints)
        {
            if (checkpoints == null || checkpoints.Count == 0)
                return false;

            var changed = false;
            for (var i = 0; i < checkpoints.Count; i++)
            {
                var checkpoint = checkpoints[i];
                if (checkpoint && checkpoint.EnsureTankReferenceInEditor(this))
                    changed = true;
            }

            return changed;
        }

        private bool EnsureCheckpointParenting(IReadOnlyList<BlitzCheckpointMarker> checkpoints)
        {
            if (checkpoints == null || checkpoints.Count == 0)
                return false;

            var changed = false;
            for (var i = 0; i < checkpoints.Count; i++)
            {
                var checkpoint = checkpoints[i];
                if (!checkpoint || checkpoint.transform == transform || transform.IsChildOf(checkpoint.transform))
                    continue;

                if (checkpoint.transform.parent == transform)
                    continue;

                Undo.SetTransformParent(checkpoint.transform, transform, "Parent Blitz Checkpoint To Tank");
                EditorUtility.SetDirty(checkpoint.transform);
                EditorUtility.SetDirty(checkpoint);
                changed = true;
            }

            return changed;
        }

        private bool SnapCheckpointsToSpline(SplineContainer spline, IReadOnlyList<BlitzCheckpointMarker> checkpoints)
        {
            if (!spline || checkpoints == null || checkpoints.Count == 0)
                return false;

            var changed = false;
            for (var i = 0; i < checkpoints.Count; i++)
            {
                var checkpoint = checkpoints[i];
                if (!checkpoint || PrefabUtility.IsPartOfPrefabAsset(checkpoint))
                    continue;

                if (!TryFindNearestPointOnSpline(spline, checkpoint.transform.position, out var snappedPosition,
                        out _))
                    continue;

                if ((checkpoint.transform.position - snappedPosition).sqrMagnitude <= 0.000001f)
                    continue;

                Undo.RecordObject(checkpoint.transform, "Snap Blitz Checkpoint To Spline");
                checkpoint.transform.position = snappedPosition;
                EditorUtility.SetDirty(checkpoint.transform);
                EditorUtility.SetDirty(checkpoint);
                changed = true;
            }

            return changed;
        }

        private bool EnsureCheckpointOrdering(IReadOnlyList<BlitzCheckpointMarker> checkpoints, SplineContainer spline)
        {
            if (checkpoints == null || checkpoints.Count == 0)
                return false;

            var ordered = new List<(BlitzCheckpointMarker checkpoint, float t, int originalIndex)>(checkpoints.Count);
            for (var i = 0; i < checkpoints.Count; i++)
            {
                var checkpoint = checkpoints[i];
                if (!checkpoint)
                    continue;

                var t = float.MaxValue;
                if (spline && TryFindNearestPointOnSpline(spline, checkpoint.transform.position, out _,
                        out var nearestT))
                    t = nearestT;

                ordered.Add((checkpoint, t, i));
            }

            ordered.Sort((left, right) =>
            {
                var tCompare = left.t.CompareTo(right.t);
                if (tCompare != 0)
                    return tCompare;

                return left.originalIndex.CompareTo(right.originalIndex);
            });

            var changed = false;
            for (var i = 0; i < ordered.Count; i++)
            {
                var checkpoint = ordered[i].checkpoint;
                if (!checkpoint || checkpoint.transform.parent != transform)
                    continue;

                if (checkpoint.transform.GetSiblingIndex() == i)
                    continue;

                Undo.RecordObject(checkpoint.transform, "Order Blitz Checkpoints");
                checkpoint.transform.SetSiblingIndex(i);
                EditorUtility.SetDirty(checkpoint.transform);
                changed = true;
            }

            return changed;
        }

        private List<BlitzCheckpointMarker> CollectAuthoringCheckpoints()
        {
            var checkpoints = new List<BlitzCheckpointMarker>();
            var seen = new HashSet<BlitzCheckpointMarker>();
            CollectCheckpointChildren(checkpoints);
            for (var i = 0; i < checkpoints.Count; i++)
                seen.Add(checkpoints[i]);

            var all = FindObjectsByType<BlitzCheckpointMarker>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var checkpoint = all[i];
                if (!checkpoint || checkpoint.Tank != this || !seen.Add(checkpoint))
                    continue;

                checkpoints.Add(checkpoint);
            }

            return checkpoints;
        }

        private static bool TryFindNearestPointOnSpline(SplineContainer spline, Vector3 worldPoint,
            out Vector3 nearest, out float nearestT)
        {
            nearest = worldPoint;
            nearestT = 0f;
            var bestDistanceSq = float.MaxValue;
            var found = false;

            for (var i = 0; i <= SplineSampleCount; i++)
            {
                var t = i / (float)SplineSampleCount;
                if (!TryEvaluateSplinePosition(spline, t, out var sampledPosition))
                    return false;

                var distanceSq = (sampledPosition - worldPoint).sqrMagnitude;
                if (!(distanceSq < bestDistanceSq))
                    continue;

                bestDistanceSq = distanceSq;
                nearest = sampledPosition;
                nearestT = t;
                found = true;
            }

            return found;
        }

        private static bool TryEvaluateSplinePosition(SplineContainer spline, float t, out Vector3 position)
        {
            position = default;
            if (!spline)
                return false;

            return spline.Evaluate(Mathf.Clamp01(t), out var splinePosition, out _, out _)
                   && TryConvertToVector3(splinePosition, out position);
        }

        private static bool TryConvertToVector3(float3 value, out Vector3 vector)
        {
            if (math.any(!math.isfinite(value)))
            {
                vector = default;
                return false;
            }

            vector = value;
            return true;
        }
#endif
    }
}