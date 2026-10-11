// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vellocet.Sdk.Editor.VSig;

namespace Vellocet.Sdk.Tests
{
    public sealed class VSigMapSourceUtilityTests
    {
        private string _assetFolder;

        [SetUp]
        public void SetUp()
        {
            var folderName = "VellocetSdkTests_" + Guid.NewGuid().ToString("N");
            _assetFolder = $"Assets/{folderName}";
            Assert.That(AssetDatabase.CreateFolder("Assets", folderName), Is.Not.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_assetFolder))
                AssetDatabase.DeleteAsset(_assetFolder);
        }

        [Test]
        public void ExternalCompiledSceneHasNoLocalSourceAndDoesNotLogAnAssetDatabaseError()
        {
            var status = VSigMapSourceUtility.FindForScene(_assetFolder + "/MissingCompilerOutput/Map.unity", out var source, out _, out _);
            Assert.That(status, Is.EqualTo(VSigMapSourceStatus.NotFound));
            Assert.That(source, Is.Null);
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FindForScene_PrefersSiblingSourceAndIgnoresVertexBackups()
        {
            var scenePath = $"{_assetFolder}/Authoring.unity";
            var siblingPath = $"{_assetFolder}/up_arcade.vsig";
            var capturedPath = $"{_assetFolder}/Source/Scripts/up_arcade.vsig";
            AssetDatabase.CreateFolder(_assetFolder, "up_arcade.vsig.backups");
            var backupPath = siblingPath + ".backups/snapshot.vsig";
            File.WriteAllText(backupPath, "# Old source\n");
            AssetDatabase.ImportAsset(backupPath, ImportAssetOptions.ForceSynchronousImport);
            Assert.That(VSigMapSourceUtility.FindForScene(scenePath, out _, out _, out _),
                Is.EqualTo(VSigMapSourceStatus.NotFound));

            AssetDatabase.CreateFolder(_assetFolder, "Source");
            AssetDatabase.CreateFolder(_assetFolder + "/Source", "Scripts");
            File.WriteAllText(capturedPath, "# Captured source\n");
            AssetDatabase.ImportAsset(capturedPath, ImportAssetOptions.ForceSynchronousImport);
            Assert.That(VSigMapSourceUtility.FindForScene(scenePath, out _, out var sourcePath, out _),
                Is.EqualTo(VSigMapSourceStatus.Found));
            Assert.That(sourcePath, Is.EqualTo(capturedPath));

            File.WriteAllText(siblingPath, "# Current source\n");
            AssetDatabase.ImportAsset(siblingPath, ImportAssetOptions.ForceSynchronousImport);
            Assert.That(VSigMapSourceUtility.FindForScene(scenePath, out var source, out sourcePath, out _),
                Is.EqualTo(VSigMapSourceStatus.Found));
            Assert.That(sourcePath, Is.EqualTo(siblingPath));
            Assert.That(source.text, Is.EqualTo("# Current source\n"));

            var otherPath = $"{_assetFolder}/Other.vsig";
            File.WriteAllText(otherPath, "# Another source\n");
            AssetDatabase.ImportAsset(otherPath, ImportAssetOptions.ForceSynchronousImport);
            Assert.That(VSigMapSourceUtility.FindForScene(scenePath, out _, out _, out _),
                Is.EqualTo(VSigMapSourceStatus.Multiple));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                Assert.That(VSigCompiledSceneInjector.Inject(scene, scenePath, null, out var message), Is.False);
                Assert.That(message, Does.Contain("Multiple VSig files").And.Contain("Authoring.vsig"));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }

            Assert.That(VSigMapSourceUtility.TryEnsureForScene(scenePath, out _, out var expectedPath, out _,
                out var error), Is.True, error);
            Assert.That(VSigMapSourceUtility.FindForScene(scenePath, out _, out sourcePath, out _),
                Is.EqualTo(VSigMapSourceStatus.Found));
            Assert.That(sourcePath, Is.EqualTo(expectedPath));
        }

        [Test]
        public void FallbackDoesNotBorrowAnotherMapsNamedScript()
        {
            var otherScene = _assetFolder + "/Other.unity";
            var script = _assetFolder + "/Other.vsig";
            File.WriteAllText(script, "# Other map's script\n");
            AssetDatabase.ImportAsset(script, ImportAssetOptions.ForceSynchronousImport);
            // Source ownership depends on the scene path, even before the scene has been imported.
            File.WriteAllText(otherScene, string.Empty);
            Assert.That(VSigMapSourceUtility.FindForScene(_assetFolder + "/Map.unity", out _, out _, out _),
                Is.EqualTo(VSigMapSourceStatus.NotFound));
            Assert.That(VSigMapSourceUtility.FindForScene(otherScene, out _, out var found, out _),
                Is.EqualTo(VSigMapSourceStatus.Found));
            Assert.That(found, Is.EqualTo(script));
            File.Delete(otherScene);
            Assert.That(VSigMapSourceUtility.FindForScene(_assetFolder + "/Map.unity", out _, out found, out _),
                Is.EqualTo(VSigMapSourceStatus.Found), "Unowned legacy source files retain their fallback behavior.");
        }

        [Test]
        public void TryEnsureForScene_CreatesMatchingVSigWithoutOverwritingIt()
        {
            var scenePath = $"{_assetFolder}/ExampleMap.unity";

            Assert.That(VSigMapSourceUtility.TryEnsureForScene(scenePath, out var source, out var sourcePath,
                out var created, out var error), Is.True, error);
            Assert.That(created, Is.True);
            Assert.That(sourcePath, Is.EqualTo($"{_assetFolder}/ExampleMap.vsig"));
            Assert.That(source, Is.Not.Null);
            Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(VSigMapSourceUtility.DefaultSourceContent));

            File.WriteAllText(sourcePath, "# Custom map logic\n");
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);

            Assert.That(VSigMapSourceUtility.TryEnsureForScene(scenePath, out source, out _, out created, out error),
                Is.True, error);
            Assert.That(created, Is.False);
            Assert.That(source.text, Is.EqualTo("# Custom map logic\n"));
            Assert.That(File.ReadAllText(sourcePath), Is.EqualTo("# Custom map logic\n"));
        }
    }
}
