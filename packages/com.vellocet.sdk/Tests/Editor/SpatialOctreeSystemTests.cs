// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vellocet.Sdk.Editor.Spatial;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Spatial;
using Vellocet.Sdk.Spatial.Acoustics;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class SpatialOctreeSystemTests
    {
        private readonly List<GameObject> _createdObjects = new();
        private Scene _testScene;

        [SetUp]
        public void SetUp()
        {
            _testScene = SceneManager.GetActiveScene();
        }

        [TearDown]
        public void TearDown()
        {
            for (var objectIndex = _createdObjects.Count - 1; objectIndex >= 0; objectIndex--)
                if (_createdObjects[objectIndex])
                    Object.DestroyImmediate(_createdObjects[objectIndex]);
            _createdObjects.Clear();
        }

        [Test]
        public void RootBounds_PreservesMapProportionsAndPadding()
        {
            var collider = CreateGameObject("MapBounds").AddComponent<BoxCollider>();
            collider.size = new Vector3(16000f, 2000f, 13000f) * 0.0254f;
            collider.transform.position = new Vector3(120f, -40f, 50f);
            Physics.SyncTransforms();
            var settings = SpatialOctreeBakeSettings.Default;
            var bounds = (Bounds)typeof(SpatialOctreeBaker)
                .GetMethod("CalculateRootBounds", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { _testScene, new[] { collider }, settings });

            Assert.That(bounds.center, Is.EqualTo(collider.bounds.center));
            Assert.That(bounds.size, Is.EqualTo(collider.bounds.size + Vector3.one * 4f));
        }

        [TestCase(1f)]
        [TestCase(0.0254f)]
        public void CreateNode_LargeOpenMapReservesBudgetForGeometry(float metersPerUnit)
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(16000f, 2000f, 13000f) * metersPerUnit);
            var settings = SpatialOctreeBakeSettings.Default;
            var nodes = BakeNodes(bounds, Array.Empty<Collider>(), settings);
            SpatialOctreeBaker.BuildNeighbors(nodes, out _, out var edges, settings.MaxBakedNeighborEdgeCount);

            Assert.That(nodes.Length, Is.LessThan(settings.MaxBakedNodeCount / 4));
            Assert.That(edges.Length, Is.LessThan(settings.MaxBakedNeighborEdgeCount / 4));
            Assert.That(nodes.Where(node => node.IsLeaf).All(node => node.IsTraversable), Is.True);
            var volume = nodes.Where(node => node.IsLeaf)
                .Sum(node => (double)node.Bounds.size.x * node.Bounds.size.y * node.Bounds.size.z);
            Assert.That(volume, Is.EqualTo((double)bounds.size.x * bounds.size.y * bounds.size.z).Within(volume * 0.00001));
            TestContext.WriteLine($"open map scale={metersPerUnit}: nodes={nodes.Length}, edges={edges.Length}");
        }

        [TestCase(1f)]
        [TestCase(0.0254f)]
        public void CreateNode_ShallowMapWithFloorFitsDefaultBudgets(float metersPerUnit)
        {
            var size = new Vector3(16000f, 2000f, 13000f) * metersPerUnit;
            var floor = CreateGameObject("Floor").AddComponent<BoxCollider>();
            floor.size = new Vector3(size.x, 1f, size.z);
            floor.transform.position = new Vector3(0f, -size.y * 0.5f, 0f);
            var ceiling = CreateGameObject("Ceiling").AddComponent<BoxCollider>();
            ceiling.size = floor.size;
            ceiling.transform.position = new Vector3(0f, size.y * 0.5f, 0f);
            Physics.SyncTransforms();
            var settings = SpatialOctreeBakeSettings.Default;
            var bounds = new Bounds(Vector3.zero, size + new Vector3(4f, 5f, 4f));
            var nodes = BakeNodes(bounds, new[] { floor, ceiling }, settings);
            SpatialOctreeBaker.BuildNeighbors(nodes, out var starts, out var edges, settings.MaxBakedNeighborEdgeCount);

            Assert.That(nodes.Length, Is.LessThan(settings.MaxBakedNodeCount));
            Assert.That(nodes.Any(node => node.IsBlocked && node.Bounds.Contains(floor.bounds.center)), Is.True);
            Assert.That(nodes.Any(node => node.IsTraversable && node.Bounds.Contains(Vector3.zero)), Is.True);
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            typeof(SpatialOctreeBaker).GetMethod("AssignComponents", flags)
                .Invoke(null, new object[] { nodes, starts, edges });
            SpatialOctreeRoutingMetadata.Build(nodes, starts, edges, settings.RoutingRegionDepth,
                out var traversable, out var regions, out var regionStarts, out var regionIndices);
            var apertures = SpatialOctreeAcousticMetadata.BuildNeighborApertures(nodes, starts, edges);
            var acoustics = SpatialOctreeAcousticMetadata.Build(nodes, starts, edges, apertures, bounds);
            var asset = CreateAsset(bounds, nodes, starts, edges, apertures, traversable, regions,
                regionStarts, regionIndices, acoustics, settings);
            try
            {
                Assert.That(SpatialOctreePerformanceBudget.TryValidate(asset, out var message), Is.True, message);
                TestContext.WriteLine($"shallow map scale={metersPerUnit}: {message}");
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void CreateNode_MergesBlockedSurfaceCellsButPreservesDoorway()
        {
            var left = CreateGameObject("LeftWall").AddComponent<BoxCollider>();
            left.size = new Vector3(6f, 0.2f, 8f);
            left.transform.position = new Vector3(-5f, 0f, 0f);
            var right = CreateGameObject("RightWall").AddComponent<BoxCollider>();
            right.size = left.size;
            right.transform.position = new Vector3(5f, 0f, 0f);
            Physics.SyncTransforms();
            var nodes = BakeNodes(new Bounds(Vector3.zero, new Vector3(16f, 0.5f, 8f)),
                new[] { left, right }, SpatialOctreeBakeSettings.Default);

            Assert.That(nodes.Length, Is.LessThan(100));
            Assert.That(nodes.Any(node => node.IsBlocked && node.Bounds.size.x > 1f && node.Bounds.size.z > 1f), Is.True);
            // Test centers of every 1-unit cell against the unmerged occupancy, including the gap.
            for (var x = -7.5f; x < 8f; x += 1f)
            for (var z = -3.5f; z < 4f; z += 1f)
            {
                var point = new Vector3(x, 0f, z);
                var leaf = nodes.Single(node => node.IsLeaf && node.Bounds.Contains(point));
                var cell = new Bounds(point, new Vector3(1f, 0.5f, 1f));
                var blocked = cell.Intersects(left.bounds) || cell.Intersects(right.bounds);
                Assert.That(leaf.IsBlocked, Is.EqualTo(blocked), $"Occupancy at {point}");
            }
        }

        [Test]
        public void OpenResolution_PreservesSmallMapSettingsAndInvalidatesCacheWhenBudgetChanges()
        {
            var settings = SpatialOctreeBakeSettings.Default;
            var small = new Bounds(Vector3.zero, Vector3.one * 16f);
            Assert.That(SpatialOctreeBaker.ResolveOpenNodeSize(small, settings), Is.EqualTo(settings.MaxOpenNodeSize));
            var large = new Bounds(Vector3.zero, new Vector3(16000f, 2000f, 13000f));
            var originalHash = SpatialOctreeBaker.ComputeGeometryHash(Array.Empty<Collider>(), settings, large, out _);
            var originalSize = SpatialOctreeBaker.ResolveOpenNodeSize(large, settings);
            settings.MaxBakedNodeCount = 1024;
            Assert.That(SpatialOctreeBaker.ResolveOpenNodeSize(large, settings), Is.GreaterThan(originalSize));
            Assert.That(SpatialOctreeBaker.ComputeGeometryHash(Array.Empty<Collider>(), settings, large, out _),
                Is.Not.EqualTo(originalHash));
        }

        [Test]
        public void BuildNeighbors_AdaptiveAxesMatchGeometricFaceAdjacency()
        {
            var collider = CreateGameObject("PartialWall").AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 3f, 4f);
            collider.transform.position = new Vector3(-3f, 0f, 1f);
            Physics.SyncTransforms();
            var settings = SpatialOctreeBakeSettings.Default;
            settings.MaxOpenNodeSize = 4f;
            var bounds = new Bounds(Vector3.zero, new Vector3(16f, 3f, 8f));
            var nodes = BakeNodes(bounds, new[] { collider }, settings);
            SpatialOctreeBaker.BuildNeighbors(nodes, out var starts, out var indices);
            Assert.That(nodes.Where(node => !node.IsLeaf).Select(node => node.ChildCount).Distinct(),
                Is.EquivalentTo(new[] { 2, 4, 8 }));

            for (var i = 0; i < nodes.Length; i++)
            {
                var expected = Enumerable.Range(0, nodes.Length).Where(j => i != j &&
                    nodes[i].IsTraversable && nodes[j].IsTraversable && ShareFace(nodes[i].Bounds, nodes[j].Bounds));
                Assert.That(indices[starts[i]..starts[i + 1]], Is.EquivalentTo(expected), $"Neighbors of node {i}");
            }

            typeof(SpatialOctreeBaker).GetMethod("AssignComponents", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { nodes, starts, indices });
            SpatialOctreeRoutingMetadata.Build(nodes, starts, indices, settings.RoutingRegionDepth,
                out var traversable, out var regions, out var regionStarts, out var regionIndices);
            var apertures = SpatialOctreeAcousticMetadata.BuildNeighborApertures(nodes, starts, indices);
            var acoustics = SpatialOctreeAcousticMetadata.Build(nodes, starts, indices, apertures, bounds);
            var asset = CreateAsset(bounds, nodes, starts, indices, apertures, traversable, regions,
                regionStarts, regionIndices, acoustics, settings);
            try
            {
                Assert.That(asset.TryValidateData(out var error), Is.True, error);
                using var graph = new SpatialOctreeGraph(asset);
                var from = new Vector3(-7f, 0f, -3f);
                var to = new Vector3(7f, 0f, 3f);
                Assert.That(graph.EstimateRoute(from, to).Succeeded, Is.True);
                using var queries = new NativeArray<SpatialOctreePathQuery>(
                    new[] { new SpatialOctreePathQuery(from, to) }, Allocator.TempJob);
                using var scheduled = graph.SchedulePathEstimates(queries, 1, Allocator.TempJob);
                scheduled.Complete();
                Assert.That(scheduled.Results[0].Succeeded, Is.True);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        private static bool ShareFace(Bounds a, Bounds b)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(a.max[axis] - b.min[axis]) > 0.001f &&
                    Mathf.Abs(b.max[axis] - a.min[axis]) > 0.001f) continue;
                var u = (axis + 1) % 3;
                var v = (axis + 2) % 3;
                if (Mathf.Min(a.max[u], b.max[u]) > Mathf.Max(a.min[u], b.min[u]) + 0.001f &&
                    Mathf.Min(a.max[v], b.max[v]) > Mathf.Max(a.min[v], b.min[v]) + 0.001f) return true;
            }
            return false;
        }

        private static SpatialOctreeNodeData[] BakeNodes(Bounds bounds, IReadOnlyList<Collider> colliders,
            SpatialOctreeBakeSettings settings)
        {
            var baker = typeof(SpatialOctreeBaker);
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var nodes = Activator.CreateInstance(typeof(List<>).MakeGenericType(
                baker.GetNestedType("MutableNode", BindingFlags.NonPublic)));
            var budget = Activator.CreateInstance(baker.GetNestedType("BakeBudgetGuard", BindingFlags.NonPublic),
                settings, System.Diagnostics.Stopwatch.StartNew());
            using var overlapTester = new SpatialOctreeBaker.CellOverlapTester();
            baker.GetMethod("CreateNode", flags).Invoke(null, new object[]
                { bounds, -1, 0, settings, colliders, nodes, overlapTester, budget });
            return (SpatialOctreeNodeData[])baker.GetMethod("FinalizeNodes", flags).Invoke(null, new[] { nodes });
        }

        [Test]
        public void CreateNode_SolidInteriorStopsBeforeExceedingSubdivisionBudget()
        {
            var collider = CreateGameObject("LargeSolid").AddComponent<BoxCollider>();
            collider.size = Vector3.one * 200f;
            Physics.SyncTransforms();

            var baker = typeof(SpatialOctreeBaker);
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var settings = SpatialOctreeBakeSettings.Default;
            settings.MaxBakedNodeCount = 1024;
            var nodes = Activator.CreateInstance(typeof(List<>).MakeGenericType(
                baker.GetNestedType("MutableNode", BindingFlags.NonPublic)));
            var budget = Activator.CreateInstance(baker.GetNestedType("BakeBudgetGuard", BindingFlags.NonPublic),
                settings, System.Diagnostics.Stopwatch.StartNew());
            using var overlapTester = new SpatialOctreeBaker.CellOverlapTester();

            baker.GetMethod("CreateNode", flags).Invoke(null, new object[]
            {
                new Bounds(Vector3.zero, Vector3.one * 128f), -1, 0, settings,
                new[] { collider }, nodes, overlapTester, budget
            });
            var result = (SpatialOctreeNodeData[])baker.GetMethod("FinalizeNodes", flags)
                .Invoke(null, new[] { nodes });

            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0].IsLeaf && result[0].IsBlocked, Is.True);
        }

        [Test]
        public void CellOverlapTester_FullBlockRequiresOneSolidContainingEveryCorner()
        {
            var collider = CreateGameObject("RotatedSolid").AddComponent<BoxCollider>();
            collider.size = new Vector3(20f, 20f, 2f);
            collider.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            Physics.SyncTransforms();

            Assert.That(SpatialOctreeBaker.CellOverlapTester.IsFullyBlocked(
                new Bounds(Vector3.zero, Vector3.one), new[] { collider }), Is.True);
            Assert.That(SpatialOctreeBaker.CellOverlapTester.IsFullyBlocked(
                new Bounds(Vector3.zero, Vector3.one * 8f), new[] { collider }), Is.False);

            collider.enabled = false;
            Assert.That(SpatialOctreeBaker.CellOverlapTester.IsFullyBlocked(
                new Bounds(Vector3.zero, Vector3.one), new[] { collider }), Is.False);
        }

        [Test]
        public void CellOverlapTester_NonConvexMeshUsesPhysicsQueryWithoutClosestPointErrors()
        {
            var geometry = CreateGameObject("SeparatedNonConvexTriangles");
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-2.5f, 0f, -0.5f),
                    new Vector3(-1.5f, 0f, -0.5f),
                    new Vector3(-2f, 0f, 0.5f),
                    new Vector3(1.5f, 0f, -0.5f),
                    new Vector3(2.5f, 0f, -0.5f),
                    new Vector3(2f, 0f, 0.5f)
                },
                triangles = new[] { 0, 1, 2, 3, 4, 5 }
            };
            mesh.RecalculateBounds();
            var meshCollider = geometry.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;

            try
            {
                Physics.SyncTransforms();
                using var overlapTester = new SpatialOctreeBaker.CellOverlapTester();
                var intersections = new List<Collider>();
                overlapTester.Collect(
                    new Bounds(Vector3.zero, Vector3.one * 0.5f),
                    new[] { meshCollider },
                    intersections,
                    SpatialOctreeBakeSettings.Default);

                Assert.That(intersections, Is.Empty);
                Assert.That(SpatialOctreeBaker.CellOverlapTester.IsFullyBlocked(
                    new Bounds(Vector3.zero, new Vector3(4f, 0f, 0.5f)), new[] { meshCollider }), Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void CollectColliders_ExcludesMovingBrushExplicitAndRigidbodyGeometry()
        {
            var staticObject = CreateGameObject("StaticGeometry");
            var staticCollider = staticObject.AddComponent<BoxCollider>();

            var doorRoot = CreateGameObject("Door");
            doorRoot.AddComponent<DoorMarker>();
            var doorChild = CreateGameObject("DoorGeometry");
            doorChild.transform.SetParent(doorRoot.transform);
            var doorCollider = doorChild.AddComponent<BoxCollider>();

            var explicitRoot = CreateGameObject("ExplicitDynamicOccluder");
            explicitRoot.AddComponent<SpatialDynamicOccluder>();
            var explicitChild = CreateGameObject("ExplicitGeometry");
            explicitChild.transform.SetParent(explicitRoot.transform);
            var explicitCollider = explicitChild.AddComponent<BoxCollider>();

            var rigidbodyObject = CreateGameObject("RigidbodyGeometry");
            var rigidbody = rigidbodyObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            var rigidbodyCollider = rigidbodyObject.AddComponent<BoxCollider>();

            Physics.SyncTransforms();
            var collected = SpatialOctreeBaker.CollectColliders(_testScene, SpatialOctreeBakeSettings.Default);

            Assert.That(collected, Is.EquivalentTo(new[] { staticCollider }));
            Assert.That(collected.Contains(doorCollider), Is.False);
            Assert.That(collected.Contains(explicitCollider), Is.False);
            Assert.That(collected.Contains(rigidbodyCollider), Is.False);
        }

        [Test]
        public void GeometryHash_DistinguishesOppositeRotationsWithMatchingWorldBounds()
        {
            var geometry = CreateGameObject("RotatedBox");
            var collider = geometry.AddComponent<BoxCollider>();
            collider.size = new Vector3(4f, 2f, 1f);
            var settings = SpatialOctreeBakeSettings.Default;
            var rootBounds = new Bounds(Vector3.zero, Vector3.one * 20f);

            geometry.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Physics.SyncTransforms();
            var positiveBounds = collider.bounds;
            var positiveHash = SpatialOctreeBaker.ComputeGeometryHash(new[] { collider }, settings, rootBounds,
                out var positiveCacheable);

            geometry.transform.rotation = Quaternion.Euler(0f, -30f, 0f);
            Physics.SyncTransforms();
            var negativeBounds = collider.bounds;
            var negativeHash = SpatialOctreeBaker.ComputeGeometryHash(new[] { collider }, settings, rootBounds,
                out var negativeCacheable);

            Assert.That(negativeBounds.center, Is.EqualTo(positiveBounds.center));
            Assert.That(negativeBounds.size.x, Is.EqualTo(positiveBounds.size.x).Within(0.0001f));
            Assert.That(negativeBounds.size.y, Is.EqualTo(positiveBounds.size.y).Within(0.0001f));
            Assert.That(negativeBounds.size.z, Is.EqualTo(positiveBounds.size.z).Within(0.0001f));
            Assert.That(positiveCacheable && negativeCacheable, Is.True);
            Assert.That(negativeHash, Is.Not.EqualTo(positiveHash));
        }

        [Test]
        public void BuildNeighbors_ConnectsMixedSizeLeavesAcrossOneFace()
        {
            var nodes = new SpatialOctreeNodeData[17];
            nodes[0] = CreateBranch(new Bounds(Vector3.zero, Vector3.one * 4f), -1, 0, 1, 8);
            for (var child = 0; child < 8; child++)
            {
                var center = new Vector3(
                    (child & 1) == 0 ? -1f : 1f,
                    (child & 2) == 0 ? -1f : 1f,
                    (child & 4) == 0 ? -1f : 1f);
                nodes[child + 1] = CreateLeaf(new Bounds(center, Vector3.one * 2f), 0, 1);
                if (child >= 2)
                {
                    nodes[child + 1].IsBlocked = true;
                    nodes[child + 1].ComponentId = -1;
                }
            }

            nodes[2] = CreateBranch(nodes[2].Bounds, 0, 1, 9, 8);
            for (var child = 0; child < 8; child++)
            {
                var center = nodes[2].Bounds.center + new Vector3(
                    (child & 1) == 0 ? -0.5f : 0.5f,
                    (child & 2) == 0 ? -0.5f : 0.5f,
                    (child & 4) == 0 ? -0.5f : 0.5f);
                nodes[child + 9] = CreateLeaf(new Bounds(center, Vector3.one), 2, 2);
            }

            SpatialOctreeBaker.BuildNeighbors(nodes, out var starts, out var indices);

            Assert.That(indices[starts[1]..starts[2]], Is.EquivalentTo(new[] { 9, 11, 13, 15 }));
            for (var neighbor = 9; neighbor <= 15; neighbor += 2)
                Assert.That(indices[starts[neighbor]..starts[neighbor + 1]], Does.Contain(1));
        }

        [Test]
        public void AcousticMetadata_UsesPerEdgeApertureAndVerticalSkyConnectivity()
        {
            var nodes = new[]
            {
                CreateLeaf(new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(2f, 1f, 2f)), -1, 0),
                CreateLeaf(new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 2f)), -1, 0),
                CreateLeaf(new Bounds(new Vector3(2f, 0.5f, 0f), new Vector3(2f, 1f, 0.5f)), -1, 0)
            };
            SetCompactIndices(nodes);
            var starts = new[] { 0, 1, 3, 4 };
            var indices = new[] { 1, 0, 2, 1 };
            var apertures = SpatialOctreeAcousticMetadata.BuildNeighborApertures(nodes, starts, indices);
            var graphBounds = new Bounds(new Vector3(1f, 1f, 0f), new Vector3(4f, 2f, 2f));

            var metadata = SpatialOctreeAcousticMetadata.Build(nodes, starts, indices, apertures, graphBounds);

            Assert.That(apertures[0], Is.EqualTo(1f).Within(0.0001f));
            Assert.That(apertures[2], Is.LessThan(0.5f));
            Assert.That(apertures[2], Is.EqualTo(apertures[3]).Within(0.0001f));
            Assert.That(metadata[0].IsExterior, Is.True);
            Assert.That(metadata[1].IsExterior, Is.True);
            Assert.That(metadata[2].IsExterior, Is.False);
            Assert.That(metadata[1].IsTransition, Is.True);
            Assert.That(metadata[2].IsTransition, Is.True);
            Assert.That(metadata[0].ClusterId, Is.EqualTo(metadata[1].ClusterId));
            Assert.That(metadata[2].ClusterId, Is.Not.EqualTo(metadata[1].ClusterId));
        }

        [Test]
        public void RoutingMetadata_GroupsConnectedLeavesByConfiguredAncestor()
        {
            var nodes = new[]
            {
                CreateBranch(new Bounds(new Vector3(1.5f, 0f, 0f), new Vector3(8f, 2f, 2f)), -1, 0, 1, 2),
                CreateBranch(new Bounds(new Vector3(0.5f, 0f, 0f), new Vector3(4f, 2f, 2f)), 0, 1, 3, 2),
                CreateBranch(new Bounds(new Vector3(2.5f, 0f, 0f), new Vector3(4f, 2f, 2f)), 0, 1, 5, 2),
                CreateLeaf(new Bounds(new Vector3(0f, 0f, 0f), Vector3.one), 1, 2),
                CreateLeaf(new Bounds(new Vector3(1f, 0f, 0f), Vector3.one), 1, 2),
                CreateLeaf(new Bounds(new Vector3(2f, 0f, 0f), Vector3.one), 2, 2),
                CreateLeaf(new Bounds(new Vector3(3f, 0f, 0f), Vector3.one), 2, 2)
            };
            var starts = new[] { 0, 0, 0, 0, 1, 3, 5, 6 };
            var indices = new[] { 4, 3, 5, 4, 6, 5 };

            SpatialOctreeRoutingMetadata.Build(nodes, starts, indices, 1, out var traversable,
                out var regions, out var regionStarts, out var regionIndices);

            Assert.That(traversable, Is.EqualTo(new[] { 3, 4, 5, 6 }));
            Assert.That(regions.Length, Is.EqualTo(2));
            Assert.That(regions.Select(region => region.NodeCount), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(regionStarts, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(regionIndices, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(nodes[3].RoutingRegionId, Is.EqualTo(nodes[4].RoutingRegionId));
            Assert.That(nodes[4].RoutingRegionId, Is.Not.EqualTo(nodes[5].RoutingRegionId));
        }

        [Test]
        public void Graph_ManagedAndScheduledHierarchicalRoutesSucceed()
        {
            var asset = CreateCompleteOpenOctreeAsset();
            try
            {
                Assert.That(asset.TryValidateData(out var validation), Is.True, validation);
                using var graph = new SpatialOctreeGraph(asset);
                var from = new Vector3(-1f, -1f, -1f);
                var to = new Vector3(1f, 1f, 1f);

                var managed = graph.EstimateRoute(from, to);

                Assert.That(managed.Succeeded, Is.True);
                Assert.That(managed.SearchIterations, Is.GreaterThan(0));
                Assert.That(managed.SearchIterations, Is.LessThanOrEqualTo(asset.BakeSettings.MaxPathSearchIterations));

                using var queries = new NativeArray<SpatialOctreePathQuery>(
                    new[] { new SpatialOctreePathQuery(from, to) }, Allocator.TempJob);
                using var scheduled = graph.SchedulePathEstimates(queries, 1, Allocator.TempJob);
                scheduled.Complete();
                var jobResult = scheduled.Results[0];

                Assert.That(jobResult.Succeeded, Is.True);
                Assert.That(jobResult.SearchIterations, Is.GreaterThan(0));
                Assert.That(jobResult.SearchIterations,
                    Is.LessThanOrEqualTo(asset.BakeSettings.MaxPathSearchIterations));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Graph_DynamicOccluderOverlayTracksColliderMovement()
        {
            var asset = CreateCompleteOpenOctreeAsset();
            var occluderObject = CreateGameObject("RuntimeDoor");
            occluderObject.transform.position = new Vector3(-1f, -1f, -1f);
            var collider = occluderObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.5f;
            Physics.SyncTransforms();

            try
            {
                using var graph = new SpatialOctreeGraph(asset, new List<Collider> { collider });
                Assert.That(graph.DynamicOccluderCount, Is.EqualTo(1));
                Assert.That(graph.DynamicBlockedNodeCount, Is.GreaterThan(0));
                Assert.That(graph.DynamicBlockedNodeCount, Is.LessThan(graph.TraversableNodeCount));

                occluderObject.transform.position = Vector3.one * 20f;
                Physics.SyncTransforms();
                graph.InvalidateDynamicOccluders();

                Assert.That(graph.DynamicBlockedNodeCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Graph_NonConvexDynamicOccluderUsesPhysicsQueryWithoutClosestPointErrors()
        {
            var asset = CreateLinearGraphAsset(4, 1024);
            var occluderObject = CreateGameObject("RuntimeNonConvexMesh");
            occluderObject.layer = 2;
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, -0.25f, -0.25f),
                    new Vector3(0f, 0.25f, -0.25f),
                    new Vector3(0f, 0f, 0.25f),
                    new Vector3(3f, -0.25f, -0.25f),
                    new Vector3(3f, 0.25f, -0.25f),
                    new Vector3(3f, 0f, 0.25f)
                },
                triangles = new[] { 0, 1, 2, 3, 4, 5 }
            };
            mesh.RecalculateBounds();
            var collider = occluderObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false;

            try
            {
                Physics.SyncTransforms();
                using var graph = new SpatialOctreeGraph(asset, new List<Collider> { collider });

                Assert.That(graph.DynamicBlockedNodeCount, Is.EqualTo(2));
                graph.InvalidateDynamicOccluders();
                Assert.That(graph.DynamicBlockedNodeCount, Is.EqualTo(2));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void PathSearchBudget_IsExplicitForManagedAndScheduledRoutes()
        {
            const int leafCount = 1300;
            var asset = CreateLinearGraphAsset(leafCount, 1024);
            try
            {
                var tightBudget = asset.BakeSettings;
                tightBudget.MaxBakedNodeCount = 1024;
                Assert.That(SpatialOctreePerformanceBudget.TryValidate(asset, tightBudget,
                    out var budgetMessage), Is.False);
                Assert.That(budgetMessage, Does.Contain("Node budget exceeded"));

                using var graph = new SpatialOctreeGraph(asset);
                var from = Vector3.zero;
                var to = new Vector3(leafCount - 1, 0f, 0f);

                var managed = graph.EstimateRoute(from, to);
                Assert.That(managed.Succeeded, Is.False);
                Assert.That(managed.FailureReason, Is.EqualTo(SpatialOctreePathFailureReason.BudgetExceeded));
                Assert.That(managed.SearchIterations, Is.EqualTo(1024));

                using var queries = new NativeArray<SpatialOctreePathQuery>(
                    new[] { new SpatialOctreePathQuery(from, to) }, Allocator.TempJob);
                using var scheduled = graph.SchedulePathEstimates(queries, 1, Allocator.TempJob);
                scheduled.Complete();
                var jobResult = scheduled.Results[0];

                Assert.That(jobResult.Succeeded, Is.False);
                Assert.That(jobResult.FailureReason, Is.EqualTo(SpatialOctreePathFailureReason.BudgetExceeded));
                Assert.That(jobResult.SearchIterations, Is.EqualTo(1024));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetValidation_RejectsAsymmetricEdgeMetadata()
        {
            var asset = CreateCompleteOpenOctreeAsset();
            try
            {
                var firstNeighbor = asset.NeighborIndexArray[0];
                var reciprocal = Array.BinarySearch(asset.NeighborIndexArray,
                    asset.NeighborStartArray[firstNeighbor],
                    asset.NeighborStartArray[firstNeighbor + 1] - asset.NeighborStartArray[firstNeighbor], 1);
                Assert.That(reciprocal, Is.GreaterThanOrEqualTo(0));
                asset.NeighborApertureArray[reciprocal] = asset.NeighborApertureArray[0] > 0.5f ? 0.25f : 0.75f;

                Assert.That(asset.TryValidateData(out var validation), Is.False);
                Assert.That(validation, Does.Contain("asymmetric aperture"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static SpatialOctreeAsset CreateCompleteOpenOctreeAsset()
        {
            var rootBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
            var nodes = new SpatialOctreeNodeData[9];
            nodes[0] = CreateBranch(rootBounds, -1, 0, 1, 8);
            for (var child = 0; child < 8; child++)
            {
                var center = new Vector3(
                    (child & 1) == 0 ? -1f : 1f,
                    (child & 2) == 0 ? -1f : 1f,
                    (child & 4) == 0 ? -1f : 1f);
                nodes[child + 1] = CreateLeaf(new Bounds(center, Vector3.one * 2f), 0, 1);
            }

            SpatialOctreeBaker.BuildNeighbors(nodes, out var starts, out var indices);
            SpatialOctreeRoutingMetadata.Build(nodes, starts, indices, 1, out var traversable,
                out var regions, out var regionStarts, out var regionIndices);
            var apertures = SpatialOctreeAcousticMetadata.BuildNeighborApertures(nodes, starts, indices);
            var acoustics = SpatialOctreeAcousticMetadata.Build(nodes, starts, indices, apertures, rootBounds);
            return CreateAsset(rootBounds, nodes, starts, indices, apertures, traversable, regions, regionStarts,
                regionIndices, acoustics, SpatialOctreeBakeSettings.Default);
        }

        private static SpatialOctreeAsset CreateLinearGraphAsset(int leafCount, int pathBudget)
        {
            var rootBounds = new Bounds(new Vector3((leafCount - 1) * 0.5f, 0f, 0f),
                new Vector3(leafCount + 1f, 2f, 2f));
            var nodes = new SpatialOctreeNodeData[leafCount + 1];
            nodes[0] = CreateBranch(rootBounds, -1, 0, 1, leafCount);
            for (var leaf = 0; leaf < leafCount; leaf++)
                nodes[leaf + 1] = CreateLeaf(new Bounds(new Vector3(leaf, 0f, 0f), Vector3.one), 0, 1);

            var starts = new int[nodes.Length + 1];
            var indexList = new List<int>((leafCount - 1) * 2);
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                starts[nodeIndex] = indexList.Count;
                if (nodeIndex == 0)
                    continue;
                if (nodeIndex > 1)
                    indexList.Add(nodeIndex - 1);
                if (nodeIndex < leafCount)
                    indexList.Add(nodeIndex + 1);
            }

            starts[^1] = indexList.Count;
            var indices = indexList.ToArray();

            SpatialOctreeRoutingMetadata.Build(nodes, starts, indices, 0, out var traversable,
                out var regions, out var regionStarts, out var regionIndices);
            var apertures = SpatialOctreeAcousticMetadata.BuildNeighborApertures(nodes, starts, indices);
            var acoustics = SpatialOctreeAcousticMetadata.Build(nodes, starts, indices, apertures, rootBounds);
            var settings = SpatialOctreeBakeSettings.Default;
            settings.MaxPathSearchIterations = pathBudget;
            return CreateAsset(rootBounds, nodes, starts, indices, apertures, traversable, regions, regionStarts,
                regionIndices, acoustics, settings);
        }

        private static SpatialOctreeAsset CreateAsset(
            Bounds rootBounds,
            SpatialOctreeNodeData[] nodes,
            int[] starts,
            int[] indices,
            float[] apertures,
            int[] traversable,
            SpatialOctreeRoutingRegionData[] regions,
            int[] regionStarts,
            int[] regionIndices,
            SpatialOctreeNodeAcousticData[] acoustics,
            SpatialOctreeBakeSettings settings)
        {
            var asset = ScriptableObject.CreateInstance<SpatialOctreeAsset>();
            asset.SetBakedData("test", "test", "test", "hash", settings, rootBounds, nodes, starts, indices,
                apertures, traversable, regions, regionStarts, regionIndices, traversable.Length, acoustics);
            Assert.That(asset.TryValidateData(out var validation), Is.True, validation);
            return asset;
        }

        private static SpatialOctreeNodeData CreateBranch(Bounds bounds, int parentIndex, int depth,
            int firstChildIndex, int childCount)
        {
            return new SpatialOctreeNodeData
            {
                Bounds = bounds,
                ParentIndex = parentIndex,
                FirstChildIndex = firstChildIndex,
                ChildCount = childCount,
                Depth = depth,
                ComponentId = -1,
                TraversableIndex = -1,
                RoutingRegionId = -1,
                IsLeaf = false,
                IsBlocked = false
            };
        }

        private static SpatialOctreeNodeData CreateLeaf(Bounds bounds, int parentIndex, int depth)
        {
            return new SpatialOctreeNodeData
            {
                Bounds = bounds,
                ParentIndex = parentIndex,
                FirstChildIndex = -1,
                ChildCount = 0,
                Depth = depth,
                ComponentId = 0,
                TraversableIndex = -1,
                RoutingRegionId = -1,
                IsLeaf = true,
                IsBlocked = false
            };
        }

        private static void SetCompactIndices(SpatialOctreeNodeData[] nodes)
        {
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                node.TraversableIndex = nodeIndex;
                node.RoutingRegionId = 0;
                nodes[nodeIndex] = node;
            }
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }
    }
}
