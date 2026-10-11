using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;
using Vertex.Contracts;
using Vertex.Unity;
using Vertex.Unity.Editor;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class VertexCollisionImportTests
    {
        [Test]
        public void ModelFilteringRetainsRenderLayersAndSharedMeshAssets()
        {
            var folder = "Assets/VertexCollisionCheck-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Object.DestroyImmediate(source.GetComponent<BoxCollider>());
                source.AddComponent<MeshCollider>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var prefab = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Prop.prefab");
                var map = new MapAsset
                {
                    MapId = Guid.NewGuid(), MetersPerUnit = 1,
                    Containers = new[] { new MapContainer { Id = Guid.NewGuid(), Name = "Prop", Model = new MapModel
                    {
                        Asset = "asset:checks/prop", CollisionFilter = new MapCollisionFilter
                            { Policy = VertexCollisionPolicy.Id, Mask = 1, IgnoreRaycasts = true }
                    } } }
                };
                using var built = VertexMapBuilder.Build(map, null, _ => prefab);
                var collider = built.Root.GetComponentsInChildren<MeshCollider>().Single(component => component.enabled);
                Assert.That(collider.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("PlayerClip")));
                Assert.That(built.Root.GetComponentInChildren<Renderer>().gameObject.layer, Is.EqualTo(0));
                Assert.That(collider.sharedMesh, Is.SameAs(prefab.GetComponent<MeshCollider>().sharedMesh));
                Assert.That(built.Root.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(collider.sharedMesh));
                Assert.That(prefab.GetComponent<MeshCollider>().enabled, Is.True);
            }
            finally { Object.DestroyImmediate(source); AssetDatabase.DeleteAsset(folder); }
        }

        [TestCase(0u, false, "RaycastOnly")]
        [TestCase(0u, true, "Default")]
        [TestCase(1u, false, "PlayerSolid")]
        [TestCase(1u, true, "PlayerClip")]
        [TestCase(2u, false, "EntitySolid")]
        [TestCase(2u, true, "EntityClip")]
        [TestCase(3u, false, "Default")]
        [TestCase(3u, true, "WorldClip")]
        public void CollisionImportPreservesEveryTargetAndRaycastCombination(uint mask, bool ignoreRaycasts, string layer)
        {
            var filter = new MapCollisionFilter { Policy = VertexCollisionPolicy.Id, Mask = mask, IgnoreRaycasts = ignoreRaycasts };
            var map = new MapAsset
            {
                MapId = Guid.NewGuid(), MetersPerUnit = 1,
                Objects = new[] { new MapObject
                {
                    Id = Guid.NewGuid(), Name = "Collision test", CollisionFilter = filter,
                    Colliders = new[] { new MapCollider { Kind = "Box", Size = new Vector3Data(1, 1, 1) } }
                } }
            };
            using var built = VertexMapBuilder.Build(map, null);
            var collider = built.Root.GetComponentInChildren<Collider>();
            Assert.That(collider != null, Is.EqualTo(mask != 0 || !ignoreRaycasts));
            Assert.That(built.Root.GetComponentInChildren<Renderer>(), Is.Null);
            if (collider)
            {
                Assert.That(collider.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer(layer)));
                Assert.That(Physics.GetIgnoreLayerCollision(collider.gameObject.layer, LayerMask.NameToLayer("Pawn")), Is.EqualTo((mask & 1) == 0));
                Assert.That(Physics.GetIgnoreLayerCollision(collider.gameObject.layer, LayerMask.NameToLayer("Item")), Is.EqualTo((mask & 2) == 0));
                Assert.That((VertexCollisionPolicy.NavigationMask & (1 << collider.gameObject.layer)) != 0, Is.EqualTo((mask & 1) != 0));
            }
            filter.Policy = "missing.collision.sdk";
            Assert.Throws<System.IO.InvalidDataException>(() => VertexMapBuilder.Build(map, null),
                "An unavailable collision binding must fail instead of silently exporting full collision.");
        }

        [Test]
        public void CompilePreservesVertexExactCollisionInsteadOfApplyingLegacyBoxHeuristic()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = Cube(scene, true);
                var exact = go.GetComponent<MeshCollider>();
                Assert.That(MapMeshOptimizationUtility.Optimize(scene, out var message), Is.True, message);
                Assert.That(go.GetComponent<MeshCollider>(), Is.SameAs(exact));
                Assert.That(exact.convex, Is.False);
                Assert.That(go.GetComponent<BoxCollider>(), Is.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void CompileRejectsDynamicExactInsteadOfSilentlyChangingItsShape()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = Cube(scene, true);
                var body = go.AddComponent<Rigidbody>(); body.isKinematic = true;
                Assert.That(MapMeshOptimizationUtility.Optimize(scene, out var message), Is.True, message);
                // Disable the collider while testing policy to avoid Unity's own activation error.
                var exact = go.GetComponent<MeshCollider>(); exact.enabled = false;
                body.isKinematic = false;
                Assert.That(MapMeshOptimizationUtility.Optimize(scene, out message), Is.False);
                Assert.That(message, Does.Contain("Choose convex/primitive proxies"));
                Assert.That(exact.convex, Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ExistingSdkScenesRetainTheirCurrentOptimizationPath()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = Cube(scene, false);
                Assert.That(MapMeshOptimizationUtility.Optimize(scene, out var message), Is.True, message);
                Assert.That(go.GetComponent<MeshCollider>(), Is.Null);
                Assert.That(go.GetComponent<BoxCollider>(), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static GameObject Cube(Scene scene, bool vertex)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(go, scene);
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            if (vertex) go.AddComponent<VertexObjectIdentity>().Initialize(Guid.NewGuid(), Guid.NewGuid(), "Authored exact cube");
            return go;
        }
    }
}
