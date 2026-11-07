// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
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
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folder));
            AssetDatabase.CreateFolder(_folder, "Compiled");
            File.WriteAllText(_folder + "/Compiled/previous.txt", "previous output");
            AssetDatabase.ImportAsset(_folder + "/Compiled/previous.txt", ImportAssetOptions.ForceSynchronousImport);
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

        [UnityTest]
        public IEnumerator SuccessfulCompilationActivatesDraftBeforePublicationAndPreservesSource() => CompileAndRollback(false);

        [UnityTest]
        public IEnumerator ExistingOutputPathsUseTheSamePublicationAndRollback() => CompileAndRollback(true);

        [UnityTest]
        public IEnumerator CompilationFromAnotherSceneRetainsTheDefinition() => CompileAndRollback(false, true);

        private IEnumerator CompileAndRollback(bool existingLayout, bool isolateSource = false)
        {
            yield return null;
            var source = _folder + "/Source.unity";
            var geometry = GameObject.CreatePrimitive(PrimitiveType.Cube);
            geometry.transform.localScale = new Vector3(10, 1, 10);
            geometry.isStatic = true;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), source);
            var original = File.ReadAllBytes(source);
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            map.id = ModeCatalog.All[0].MapIdPrefix + "_compile_test";
            map.displayName = "Native compile test";
            map.authoringScenePath = source;
            AssetDatabase.CreateAsset(map, _folder + "/MapDefinition.asset");
            var scenePath = existingLayout ? _folder + "/existing.unity" : null;
            var receiptPath = existingLayout ? _folder + "/Generated/compilation.json" : null;
            string sceneGuid = null;
            if (existingLayout)
            {
                File.Copy(source, scenePath);
                AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
                sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                Directory.CreateDirectory(_folder + "/Generated");
                File.WriteAllText(receiptPath, "previous receipt");
            }
            var published = false;
            var settings = new BaseMapSettings
            {
                Publish = compiled =>
                {
                    if (existingLayout)
                    {
                        Assert.That(compiled.compiledScenePath, Is.EqualTo(scenePath));
                        Assert.That(compiled.compilationReceiptPath, Is.EqualTo(receiptPath));
                        Assert.That(compiled.sceneReference.AssetGUID, Is.EqualTo(sceneGuid));
                    }
                    Assert.That(compiled.Enabled, Is.True);
                    Assert.That(compiled.IsDraft, Is.False);
                    Assert.That(BaseMapAuthoring.TryValidateCompiled(compiled, out var error), Is.True, error);
                    Assert.That(BaseMapArtifactValidator.TryValidate(compiled, out error,
                        new BaseMapValidationOptions { AllowSkippedSpatialOctreeBake = false }), Is.False);
                    Assert.That(error, Does.Contain("bake was skipped"));
                    Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
                    var compiledBytes = File.ReadAllBytes(compiled.compiledScenePath);
                    File.AppendAllText(compiled.compiledScenePath, "\n");
                    Assert.That(BaseMapAuthoring.TryValidateCompiled(compiled, out error), Is.False);
                    Assert.That(error, Does.Contain("changed since compilation"));
                    File.WriteAllBytes(compiled.compiledScenePath, compiledBytes);
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

            yield return null;
            if (isolateSource)
                EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single),
                    _folder + "/Other.unity");
            Assert.Throws<OperationCanceledException>(() =>
                BaseMapCompile.Compile(settings, map, source, options, keepsSource: true, scenePath, receiptPath));

            Assert.That(published, Is.True, "The native compiler must complete and reach publication.");
            if (existingLayout)
            {
                Assert.That(File.ReadAllBytes(scenePath), Is.EqualTo(original));
                Assert.That(File.ReadAllText(receiptPath), Is.EqualTo("previous receipt"));
                Assert.That(AssetDatabase.AssetPathToGUID(scenePath), Is.EqualTo(sceneGuid));
            }
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
            map = AssetDatabase.LoadAssetAtPath<MapDefinition>(_folder + "/MapDefinition.asset");
            Assert.That(map.IsDraft, Is.True, "A failed publication must restore the draft definition.");
            Assert.That(File.ReadAllText(_folder + "/Compiled/previous.txt"), Is.EqualTo("previous output"));
        }

        [UnityTest]
        public IEnumerator PreparationSeesPreviousOutputAndFailureLeavesItIntact()
        {
            yield return null;
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

            yield return null;
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
