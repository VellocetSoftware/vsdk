// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class BaseMapCompileTests
    {
        private string _folder;
        private SceneSetup[] _setup;

        [SetUp]
        public void SetUp()
        {
            _folder = null;
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                Assert.Ignore("Save open scenes before running scene tests.");
            _setup = EditorSceneManager.GetSceneManagerSetup();
            _folder = "Assets/BaseMapCompileTest_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(_folder + "/Compiled");
            File.WriteAllText(_folder + "/Compiled/previous.txt", "previous output");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), _folder + "/Source.unity");
        }

        [TearDown]
        public void TearDown()
        {
            if (_folder == null) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(_folder);
            if (_setup.Length > 0 && System.Array.TrueForAll(_setup, scene => !string.IsNullOrEmpty(scene.path) && File.Exists(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
        }

        [Test]
        public void SuccessfulCompilationActivatesDraftBeforePublicationAndPreservesSource()
        {
            var source = _folder + "/Source.unity";
            var original = File.ReadAllBytes(source);
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            map.id = ModeCatalog.All[0].MapIdPrefix + "_compile_test";
            map.displayName = "Native compile test";
            map.authoringScenePath = source;
            AssetDatabase.CreateAsset(map, _folder + "/MapDefinition.asset");
            var published = false;
            var settings = new BaseMapSettings
            {
                Publish = compiled =>
                {
                    Assert.That(compiled.Enabled, Is.True);
                    Assert.That(compiled.IsDraft, Is.False);
                    Assert.That(BaseMapAuthoring.TryValidateCompiled(compiled, out var error), Is.True, error);
                    Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
                    published = true;
                    // Exercise successful compilation without leaving project Addressables changes behind.
                    throw new OperationCanceledException("Restore the test publication.");
                }
            };
            var options = new MapCompiler.CompileOptions
            {
                SkipLightingBake = true,
                SkipSpatialOctreeBake = true,
                Log = TestContext.WriteLine
            };

            Assert.Throws<OperationCanceledException>(() =>
                BaseMapCompile.Compile(settings, map, source, options, keepsSource: true));

            Assert.That(published, Is.True, "The native compiler must complete and reach publication.");
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
            map = AssetDatabase.LoadAssetAtPath<MapDefinition>(_folder + "/MapDefinition.asset");
            Assert.That(map.IsDraft, Is.True, "A failed publication must restore the draft definition.");
            Assert.That(File.ReadAllText(_folder + "/Compiled/previous.txt"), Is.EqualTo("previous output"));
        }

        [Test]
        public void PreparationSeesPreviousOutputAndFailureLeavesItIntact()
        {
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            map.id = ModeCatalog.All[0].MapIdPrefix + "_rollback_test";
            map.compiledScenePath = _folder + "/Compiled/previous.unity";
            AssetDatabase.CreateAsset(map, _folder + "/MapDefinition.asset");
            var guid = AssetDatabase.AssetPathToGUID(_folder + "/Compiled/previous.txt");
            var sawPreviousOutput = false;
            var settings = new BaseMapSettings
            {
                Prepare = () =>
                {
                    sawPreviousOutput = File.Exists(_folder + "/Compiled/previous.txt");
                    throw new InvalidDataException("prepare failed");
                }
            };

            Assert.Throws<InvalidDataException>(() => BaseMapCompile.Compile(settings, map, _folder + "/Source.unity",
                new MapCompiler.CompileOptions(), keepsSource: true));

            Assert.That(sawPreviousOutput, Is.True, "Preparation must see the published output before it is backed up.");
            Assert.That(File.ReadAllText(_folder + "/Compiled/previous.txt"), Is.EqualTo("previous output"));
            Assert.That(AssetDatabase.AssetPathToGUID(_folder + "/Compiled/previous.txt"), Is.EqualTo(guid));
            var restored = AssetDatabase.LoadAssetAtPath<MapDefinition>(_folder + "/MapDefinition.asset");
            Assert.That(restored.compiledScenePath, Is.EqualTo(_folder + "/Compiled/previous.unity"));
            Assert.That(Directory.Exists("Library/VellocetBaseMaps/" + map.id), Is.False);
        }
    }
}
