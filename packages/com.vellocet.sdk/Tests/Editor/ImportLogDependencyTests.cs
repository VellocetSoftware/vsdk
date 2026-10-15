using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Stereoform;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class ImportLogDependencyTests
    {
        [Test]
        public void EditorImportLogsDoNotRejectValidRuntimeAudioDependencies()
        {
            var root = "Assets/ImportLogDependencyCheck-" + Guid.NewGuid().ToString("N");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
                var reference = ScriptableObject.CreateInstance<AudioEventReference>();
                reference.ConfigureEvent("core", "test/import-log-dependency", .5f);
                var path = root + "/Reference.asset";
                AssetDatabase.CreateAsset(reference, path);
                reference.name = "Friendly audio display name";
                EditorUtility.SetDirty(reference);
                AssetDatabase.SaveAssetIfDirty(reference);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var metadata = AssetDatabase.LoadAllAssetsAtPath(path)
                    .Single(asset => asset && asset.GetType().FullName == "UnityEditor.AssetImporters.ImportLog");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, root + "/Dependency.unity");
                var dependencies = new[] { new MapCompiler.RuntimeDependency(AssetDatabase.AssetPathToGUID(path), "tests/import-log") };
                Assert.That(AddonScenePolicy.TryValidate(scene, dependencies, out var error), Is.True, error);
                Assert.That(AddonScenePolicy.TryValidate(scene, dependencies, out error, new[] { metadata }), Is.False,
                    "Editor metadata must still be rejected if it enters the player dependency list.");
                Assert.That(error, Does.Contain("unsupported asset type"));
                var importerPath = "Packages/com.vellocet.vertex/Editor/VertexMapImporter.cs";
                var importer = AssetDatabase.LoadAssetAtPath<MonoScript>(importerPath);
                Assert.That(importer, Is.Not.Null);
                var importerDependency = new[] { new MapCompiler.RuntimeDependency(
                    AssetDatabase.AssetPathToGUID(importerPath), "tests/source-importer") };
                Assert.That(AddonScenePolicy.TryValidate(scene, importerDependency, out error), Is.True, error);
                Assert.That(AddonScenePolicy.TryValidate(scene, importerDependency, out error,
                    new Object[] { importer }), Is.False);
                Assert.That(error, Does.Contain("forbidden script"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(root);
                if (setup.Any(scene => scene.isActive && scene.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
