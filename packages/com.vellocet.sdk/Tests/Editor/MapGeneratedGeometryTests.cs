// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;
using Vertex.Contracts;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class MapGeneratedGeometryTests
    {
        [SetUp]
        public void StartWithEmptyScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void CompiledRenderAndCollisionMeshesSurviveRemovalOfTheirVertexSource()
        {
            var previous = SceneManager.GetActiveScene();
            var folder = "Assets/CompiledGeometryCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var scene = MapTestScenes.CreateEmpty();
            var scenePath = folder + "/Compiled.unity";
            try
            {
                var map = new MapAsset
                {
                    MapId = Guid.NewGuid(), MetersPerUnit = 1,
                    Objects = new[] { new MapObject
                    {
                        Id = Guid.NewGuid(), Name = "World triangle",
                        Vertices = new[] { Vertex(0, 0), Vertex(0, 2), Vertex(3, 0) },
                        Surfaces = new[] { new MapSurface { Material = "builtin:checker", Indices = new[] { 0, 1, 2 } } },
                        Lightmap = new MapLightmap { GenerateUV2 = true, ResolutionScale = 1 }
                    } }
                };
                var sourcePath = folder + "/Map.vertexmesh";
                File.WriteAllBytes(sourcePath, map.Serialize());
                AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                Assert.That(prefab, Is.Not.Null);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var filter = instance.GetComponentInChildren<MeshFilter>();
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                var uv2 = filter.sharedMesh.uv2;
                var vertices = filter.sharedMesh.vertices;
                Assert.That(uv2.Length, Is.EqualTo(vertices.Length));
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(MapGeneratedGeometryUtility.Persist(scene, scenePath, out var message), Is.True, message);
                Assert.That(filter.sharedMesh, Is.SameAs(collider.sharedMesh), "Shared geometry stays shared.");
                Assert.That(filter.sharedMesh.uv2, Is.EqualTo(uv2), "Existing lightmap UVs must remain intact.");
                Assert.That(MapGeneratedGeometryUtility.Persist(scene, scenePath, out message), Is.True, message);
                Assert.That(MapGeneratedGeometryUtility.FinalizeMeshes(scene, scenePath, out message), Is.True, message);
                Assert.That(filter.sharedMesh.isReadable, Is.True, "Collision meshes remain readable.");
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                MapTestScenes.Close(scene);
                Assert.That(AssetDatabase.DeleteAsset(sourcePath), Is.True);
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                filter = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>()).Single();
                collider = filter.GetComponent<MeshCollider>();
                Assert.That(filter.sharedMesh, Is.Not.Null);
                Assert.That(filter.sharedMesh.vertices, Is.EqualTo(vertices));
                Assert.That(filter.sharedMesh.uv2, Is.EqualTo(uv2));
                Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh));
                Assert.That(AssetDatabase.GetDependencies(scenePath, true).Any(p => p.EndsWith(".vertexmesh")), Is.False);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) MapTestScenes.Close(scene);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(false, 1, false, false)]
        [TestCase(true, 1, false, true)]
        [TestCase(false, 2, false, true)]
        [TestCase(false, 1, true, true)]
        public void FinalizationReleasesOnlyRenderMeshesWithoutRuntimeReaders(
            bool collision, int materialCount, bool moving, bool expectedReadable)
        {
            var previous = SceneManager.GetActiveScene();
            var folder = "Assets/CompiledReadabilityCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var scene = MapTestScenes.CreateEmpty();
            try
            {
                var sourcePath = folder + "/Map.vertexmesh";
                File.WriteAllBytes(sourcePath, new MapAsset
                {
                    MapId = Guid.NewGuid(), MetersPerUnit = 1,
                    Objects = new[] { new MapObject
                    {
                        Id = Guid.NewGuid(), Name = "World triangle",
                        Vertices = new[] { Vertex(0, 0), Vertex(0, 2), Vertex(3, 0) },
                        Surfaces = Enumerable.Range(0, materialCount).Select(_ => new MapSurface
                            { Material = "builtin:checker", Indices = new[] { 0, 1, 2 } }).ToArray(),
                        Lightmap = new MapLightmap { GenerateUV2 = true, ResolutionScale = 1 }
                    } }
                }.Serialize());
                AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath), scene);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var filter = instance.GetComponentInChildren<MeshFilter>();
                if (collision) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                if (moving) filter.gameObject.AddComponent<Rigidbody>().isKinematic = true;
                var uv2 = filter.sharedMesh.uv2;
                var scenePath = folder + "/Compiled.unity";
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(MapGeneratedGeometryUtility.Persist(scene, scenePath, out var message), Is.True, message);
                Assert.That(MapGeneratedGeometryUtility.FinalizeMeshes(scene, scenePath, out message), Is.True, message);
                Assert.That(filter.sharedMesh.isReadable, Is.EqualTo(expectedReadable));
                var finalizedPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                AssetDatabase.ImportAsset(finalizedPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Assert.That(filter.sharedMesh.isReadable, Is.EqualTo(expectedReadable), "Readability survives serialization.");
                Assert.That(filter.sharedMesh.uv2, Is.EqualTo(uv2), "Finalization preserves baked lightmap UVs.");
            }
            finally
            {
                MapTestScenes.Close(scene);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void MissingStaticWorldMeshFailsBeforeBaking()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = MapTestScenes.CreateEmpty();
            try
            {
                var world = new GameObject("Missing wall", typeof(MeshFilter), typeof(MeshRenderer));
                SceneManager.MoveGameObjectToScene(world, scene);
                GameObjectUtility.SetStaticEditorFlags(world, StaticEditorFlags.ContributeGI);
                Assert.That(MapGeneratedGeometryUtility.Validate(scene, out var message), Is.False);
                Assert.That(message, Does.Contain("Missing wall"));
            }
            finally
            {
                MapTestScenes.Close(scene);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static MapVertex Vertex(double x, double z) => new()
        {
            Position = new Vector3Data(x, 0, z), Normal = new Vector3Data(0, 1, 0), U = x, V = z
        };
    }
}
#endif
