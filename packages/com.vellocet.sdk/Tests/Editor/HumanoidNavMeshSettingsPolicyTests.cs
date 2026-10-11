// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class HumanoidNavMeshSettingsPolicyTests
    {
        [TestCase(PrimitiveType.Cube)]
        [TestCase(PrimitiveType.Plane)]
        public void NavigationAndOcclusionBakes_PersistAcrossSceneReload(PrimitiveType primitive)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var folder = AssetDatabase.GenerateUniqueAssetPath("Assets/VertexNavBakeCheck");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var floor = GameObject.CreatePrimitive(primitive);
                if (primitive == PrimitiveType.Plane)
                {
                    // Unlike Unity's built-in plane, an exported floor face has exactly zero height.
                    var mesh = Object.Instantiate(floor.GetComponent<MeshFilter>().sharedMesh);
                    mesh.vertices = mesh.vertices.Select(vertex => new Vector3(vertex.x, 0, vertex.z)).ToArray();
                    mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh, folder + "/Floor.asset");
                    floor.GetComponent<MeshFilter>().sharedMesh = mesh;
                    floor.GetComponent<MeshCollider>().sharedMesh = mesh;
                }
                floor.transform.localScale = primitive == PrimitiveType.Plane ? new Vector3(1.2f, 1, 1.2f) : new Vector3(12, .2f, 12);
                floor.transform.position = primitive == PrimitiveType.Plane ? new Vector3(0, 1.8288f, 0) : new Vector3(0, -.1f, 0);
                GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                SceneManager.MoveGameObjectToScene(floor, scene);
                Assert.That(EditorSceneManager.SaveScene(scene, folder + "/Map.unity"), Is.True);
                Assert.That(MapSceneUtility.BakeNavigation(scene, out var error), Is.True, error);
                var data = MapSceneUtility.GetNavigationData(scene);
                Assert.That(data, Is.Not.Null);
                Assert.That(EditorUtility.IsPersistent(data), Is.True, "An in-memory NavMesh disappears when the compiler closes its scene.");
                var path = AssetDatabase.GetAssetPath(data);
                Assert.That(MapSceneUtility.BakeOcclusion(scene, out error), Is.True, error);
                Assert.That(MapSceneUtility.HasBakedOcclusion(), Is.True);
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OcclusionArea>()), Is.Empty,
                    "Temporary bake bounds must not add objects to the saved map.");
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene = EditorSceneManager.OpenScene(folder + "/Map.unity", OpenSceneMode.Single);
                Assert.That(AssetDatabase.GetAssetPath(MapSceneUtility.GetNavigationData(scene)), Is.EqualTo(path));
                Assert.That(MapSceneUtility.HasBakedOcclusion(), Is.True, "Validation uses the persisted occlusion artifact after a batch scene reload.");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
                if (setup.Any(item => item.isActive && item.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        [Test]
        public void ProjectHumanoidSettings_MatchSdkPlayerContract()
        {
            Assert.That(
                HumanoidNavMeshSettingsPolicy.IsCurrent(out var mismatch),
                Is.True,
                mismatch);
        }

        [Test]
        public void PlayerContract_UsesExpectedPhysicalDimensionsAndDropHeight()
        {
            Assert.That(HumanoidNavMeshSettingsPolicy.AgentRadius, Is.EqualTo(0.5f));
            Assert.That(HumanoidNavMeshSettingsPolicy.AgentHeight, Is.EqualTo(2f));
            Assert.That(HumanoidNavMeshSettingsPolicy.AgentSlope, Is.EqualTo(45f));
            Assert.That(HumanoidNavMeshSettingsPolicy.AgentClimb, Is.EqualTo(0.35f));
            Assert.That(HumanoidNavMeshSettingsPolicy.LedgeDropHeight, Is.EqualTo(8f));
            Assert.That(HumanoidNavMeshSettingsPolicy.MaxJumpAcrossDistance, Is.EqualTo(1f));
            Assert.That(HumanoidNavMeshSettingsPolicy.MinRegionArea, Is.EqualTo(2f));
            Assert.That(HumanoidNavMeshSettingsPolicy.ManualCellSize, Is.False);
            Assert.That(HumanoidNavMeshSettingsPolicy.CellSize, Is.EqualTo(1f / 6f));
            Assert.That(HumanoidNavMeshSettingsPolicy.ManualTileSize, Is.False);
            Assert.That(HumanoidNavMeshSettingsPolicy.TileSize, Is.EqualTo(256));
            Assert.That(HumanoidNavMeshSettingsPolicy.BuildHeightMesh, Is.False);
            Assert.That(HumanoidNavMeshSettingsPolicy.MaxJobWorkers, Is.Zero);
            Assert.That(HumanoidNavMeshSettingsPolicy.PreserveTilesOutsideBounds, Is.False);
        }

        [Test]
        public void CompilerPrepareScene_EnforcesEveryOptimalHumanoidSurfaceSetting()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var context = MapSceneUtility.EnsureSceneContract(scene, false, out _);
                var surface = context.NavigationSurface;
                Assert.That(surface, Is.Not.Null);

                var serialized = new SerializedObject(surface);
                serialized.FindProperty("m_CollectObjects").intValue = 2;
                serialized.FindProperty("m_LayerMask").intValue = 1;
                serialized.FindProperty("m_UseGeometry").intValue = 0;
                serialized.FindProperty("m_DefaultArea").intValue = 1;
                serialized.FindProperty("m_GenerateLinks").boolValue = false;
                serialized.FindProperty("m_IgnoreNavMeshAgent").boolValue = false;
                serialized.FindProperty("m_IgnoreNavMeshObstacle").boolValue = false;
                serialized.FindProperty("m_OverrideTileSize").boolValue = true;
                serialized.FindProperty("m_TileSize").intValue = 32;
                serialized.FindProperty("m_OverrideVoxelSize").boolValue = true;
                serialized.FindProperty("m_VoxelSize").floatValue = 1f;
                serialized.FindProperty("m_MinRegionArea").floatValue = 0f;
                serialized.FindProperty("m_BuildHeightMesh").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ((Behaviour)surface).enabled = false;
                surface.gameObject.SetActive(false);

                var compileContext = new MapCompileContext(
                    null,
                    "Assets/Source.unity",
                    "Assets/Compiled.unity",
                    null,
                    new MapCompiler.CompileOptions(),
                    new MapCompiler.Result(),
                    new MapCompilationReceipt { contentKind = MapCompilationReceipt.AddonContentKind })
                {
                    Scene = scene
                };
                var prepareScene = MapCompilePassFactory.Create(compileContext).First();
                Assert.That(prepareScene.Name, Is.EqualTo("prepare-scene"));

                var result = prepareScene.Execute(compileContext);
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(MapSceneUtility.TryGetContext(scene, out context), Is.True);

                Assert.That(result.Message, Does.Contain("Enforced optimal Humanoid NavMeshSurface settings"));
                Assert.That(context.NavigationSurface, Is.SameAs(surface));
                Assert.That(
                    MapSceneUtility.TryValidateHumanoidNavigationSurface(surface, out var mismatch),
                    Is.True,
                    mismatch);
                Assert.That(((Behaviour)surface).enabled, Is.True);
                Assert.That(surface.gameObject.activeInHierarchy, Is.True);

                serialized.Update();
                Assert.That(serialized.FindProperty("m_GenerateLinks").boolValue, Is.True);
                Assert.That(serialized.FindProperty("m_LayerMask").intValue, Is.EqualTo(SdkGame.Current.NavigationMask));
                Assert.That(serialized.FindProperty("m_UseGeometry").intValue, Is.EqualTo(1));
                Assert.That(serialized.FindProperty("m_TileSize").intValue, Is.EqualTo(256));
                Assert.That(serialized.FindProperty("m_VoxelSize").floatValue, Is.EqualTo(1f / 6f));
            }
            finally
            {
                if (scene.IsValid())
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
