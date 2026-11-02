// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class BaseMapCompileTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/BaseMapCompileTest_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(_folder + "/Compiled");
            File.WriteAllText(_folder + "/Compiled/previous.txt", "previous output");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), _folder + "/Source.unity");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(_folder);

        [Test]
        public void PreparationSeesPreviousOutputAndFailureLeavesItIntact()
        {
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            map.id = "nm_rollback_test";
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
            Assert.That(Directory.Exists("Library/VellocetBaseMaps/nm_rollback_test"), Is.False);
        }
    }
}
