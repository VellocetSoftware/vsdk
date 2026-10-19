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
        [Test]
        public void CompiledRenderAndCollisionMeshesSurviveRemovalOfTheirVertexSource()
        {
            var previous = SceneManager.GetActiveScene();
            var folder = "Assets/CompiledGeometryCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
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
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                EditorSceneManager.CloseScene(scene, true);
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
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void MissingStaticWorldMeshFailsBeforeBaking()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
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
                EditorSceneManager.CloseScene(scene, true);
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
