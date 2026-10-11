// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Entities;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class AddonScenePolicyTests
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void SupportedSceneDataPasses()
        {
            var root = new GameObject("Root", typeof(BoxCollider), typeof(SpawnMarker));
            SceneManager.MoveGameObjectToScene(root, _scene);

            Assert.That(AddonScenePolicy.TryValidate(_scene,
                Array.Empty<MapCompiler.RuntimeDependency>(), out var error), Is.True, error);
            var cue = ScriptableObject.CreateInstance<Vellocet.Sdk.Audio.AddonAudioCue>();
            try
            {
                Assert.That(AddonScenePolicy.TryValidate(_scene, Array.Empty<MapCompiler.RuntimeDependency>(), out error,
                    new UnityEngine.Object[] { cue, UnityEditor.MonoScript.FromScriptableObject(cue) }), Is.True, error);
            }
            finally { UnityEngine.Object.DestroyImmediate(cue); }
        }

        [Test]
        public void PackagedTerrainBlendPassesWithoutAllowingCreatorShaders()
        {
            const string source = "Packages/com.vellocet.vertex/Runtime/VertexTerrainBlend.shader";
            var copy = AssetDatabase.GenerateUniqueAssetPath("Assets/TerrainBlendPolicyTest.shader");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(source);
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(AddonScenePolicy.TryValidate(_scene, Array.Empty<MapCompiler.RuntimeDependency>(),
                    out var error, new UnityEngine.Object[] { material, shader }), Is.True, error);
                Assert.That(AssetDatabase.CopyAsset(source, copy), Is.True);
                AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
                material.shader = AssetDatabase.LoadAssetAtPath<Shader>(copy);
                Assert.That(material.shader, Is.Not.Null);
                Assert.That(AddonScenePolicy.TryValidate(_scene, Array.Empty<MapCompiler.RuntimeDependency>(),
                    out error, new UnityEngine.Object[] { material }), Is.False);
                Assert.That(error, Does.Contain(copy));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                AssetDatabase.DeleteAsset(copy);
            }
        }

        [Test]
        public void ArbitraryBehavioursAreRejected()
        {
            var root = new GameObject("Root", typeof(UnsafeAddonSceneTestBehaviour));
            SceneManager.MoveGameObjectToScene(root, _scene);

            Assert.That(AddonScenePolicy.TryValidate(_scene,
                Array.Empty<MapCompiler.RuntimeDependency>(), out var error), Is.False);
            Assert.That(error, Does.Contain(typeof(UnsafeAddonSceneTestBehaviour).FullName));
        }
    }

    internal sealed class UnsafeAddonSceneTestBehaviour : MonoBehaviour
    {
    }
}
#endif
