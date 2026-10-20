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
using Vellocet.Sdk.Editor.Maps;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class MapBakeArtifactSyncTests
    {
        [Test]
        public void ProbeLightingTransfersBothWays_AndSurvivesSceneReload()
        {
            if (!MapProbeVolumeUtility.IsAdaptiveProbeVolumeEnabled(out var reason))
                Assert.Ignore(reason);

            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var previousLightmaps = LightmapSettings.lightmaps;
            var root = "Assets/MapBakeSyncCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
            try
            {
                var compiledPath = root + "/Compiled.unity";
                var authoringPath = root + "/Authoring.unity";
                foreach (var path in new[] { authoringPath, compiledPath })
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                    GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                    EditorSceneManager.CloseScene(scene, true);
                }

                var compiled = EditorSceneManager.OpenScene(compiledPath, OpenSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(compiled);
                Assert.That(MapProbeVolumeUtility.ConfigureForLightingBake(compiled, compiledPath, out var message),
                    Is.True, message);
                var setPath = root + "/Compiled/Compiled Baking Set.asset";
                MapProbeVolumeUtility.ApplyBakedData(compiled, setPath);
                var set = MapProbeVolumeUtility.GetBakedData(compiled);
                var payloadPath = root + "/Compiled/Compiled Baking Set.CellSharedData.bytes";
                File.WriteAllText(payloadPath, "probe payload");
                AssetDatabase.ImportAsset(payloadPath);
                var serialized = new SerializedObject(set);
                var shared = serialized.FindProperty("cellSharedDataAsset");
                shared.FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(payloadPath);
                shared.FindPropertyRelative("m_Asset").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(payloadPath);
                shared.FindPropertyRelative("m_StreamableAssetPath").stringValue = "APVStreamingAssets/" +
                    AssetDatabase.AssetPathToGUID(setPath) + "/" + AssetDatabase.AssetPathToGUID(payloadPath) + ".bytes";
                var scenarioKeys = serialized.FindProperty("scenarios.m_Keys");
                var scenarioValues = serialized.FindProperty("scenarios.m_Values");
                scenarioKeys.arraySize = scenarioValues.arraySize = 1;
                scenarioKeys.GetArrayElementAtIndex(0).stringValue =
                    serialized.FindProperty("m_LightingScenarios").GetArrayElementAtIndex(0).stringValue;
                var scenarioData = scenarioValues.GetArrayElementAtIndex(0).FindPropertyRelative("cellDataAsset");
                scenarioData.FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(payloadPath);
                scenarioData.FindPropertyRelative("m_Asset").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(payloadPath);
                serialized.FindProperty("settings.virtualOffsetSettings.searchMultiplier").floatValue = 0.35f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(compiled);
                AssetDatabase.SaveAssets();

                var payloadGuid = AssetDatabase.AssetPathToGUID(payloadPath);
                var expectedPayload = root + "/Generated/Bakes/" + AssetDatabase.AssetPathToGUID(compiledPath) +
                                      "/Compiled Baking Set.CellSharedData.bytes";

                // Forward after a bake, then back into a compile that preserves existing lighting.
                foreach (var targetPath in new[] { authoringPath, compiledPath })
                {
                    var source = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    var sourceSet = MapProbeVolumeUtility.GetBakedData(source);
                    Assert.That(File.Exists(ProbePayloadPath(sourceSet)), Is.True);
                    Assert.That(File.Exists(ProbePayloadPath(sourceSet, true)), Is.True);
                    MapBakeArtifactFileMirror.Result mirror;
                    var mirrored = targetPath == authoringPath
                        ? MapBakeArtifactFileMirror.CopyCompiledToAuthoring(source.path, targetPath, out mirror, out message)
                        : MapBakeArtifactFileMirror.CopyAuthoringToCompiled(source.path, targetPath, out mirror, out message);
                    Assert.That(mirrored, Is.True, message);
                    Assert.That(ProbePayloadPath(sourceSet), Is.EqualTo(expectedPayload),
                        "Unity's build processor must not retain the pre-move path.");
                    Assert.That(ProbePayloadPath(sourceSet, true), Is.EqualTo(expectedPayload),
                        "Per-scenario probe payload paths must refresh too.");
                    var snapshot = MapCompiledBakeSnapshot.Capture(source, mirror.AssetMap);
                    EditorSceneManager.CloseScene(source, true);
                    var target = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Additive);
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(target);
                    snapshot.Apply(target, MapCompiledBakeSnapshot.ApplyOptions.LightingOnly);
                    snapshot.Apply(target, MapCompiledBakeSnapshot.ApplyOptions.LightingOnly);
                    Assert.That(EditorSceneManager.SaveScene(target), Is.True);
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.CloseScene(target, true);
                    target = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Additive);
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(target);

                    var name = Path.GetFileNameWithoutExtension(targetPath);
                    var expectedSet = root + "/" + name + "/" + name + " Baking Set.asset";
                    Assert.That(AssetDatabase.AssetPathToGUID(expectedPayload), Is.EqualTo(payloadGuid));
                    Assert.That(Directory.GetFiles(root, "*.bytes", SearchOption.AllDirectories),
                        Has.Length.EqualTo(1), "Source and compiled scenes must share one physical payload.");
                    set = MapProbeVolumeUtility.GetBakedData(target);
                    Assert.That(AssetDatabase.GetAssetPath(set), Is.EqualTo(expectedSet));
                    var components = target.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true));
                    Assert.That(components.Count(c => c && c.GetType().Name == "ProbeVolumePerSceneData"), Is.EqualTo(1));
                    serialized = new SerializedObject(set);
                    Assert.That(serialized.FindProperty("m_SceneGUIDs").GetArrayElementAtIndex(0).stringValue,
                        Is.EqualTo(AssetDatabase.AssetPathToGUID(targetPath)));
                    Assert.That(serialized.FindProperty("m_SceneBakeData.m_Values").GetArrayElementAtIndex(0)
                        .FindPropertyRelative("hasProbeVolume").boolValue, Is.True);
                    Assert.That(serialized.FindProperty("settings.virtualOffsetSettings.searchMultiplier").floatValue,
                        Is.EqualTo(0.35f), "Transferring a bake must preserve its settings.");
                    shared = serialized.FindProperty("cellSharedDataAsset");
                    Assert.That(shared.FindPropertyRelative("m_AssetGUID").stringValue,
                        Is.EqualTo(AssetDatabase.AssetPathToGUID(expectedPayload)));
                    Assert.That(AssetDatabase.GetAssetPath(shared.FindPropertyRelative("m_Asset").objectReferenceValue),
                        Is.EqualTo(expectedPayload));
                    Assert.That(shared.FindPropertyRelative("m_StreamableAssetPath").stringValue,
                        Is.EqualTo("APVStreamingAssets/" + AssetDatabase.AssetPathToGUID(expectedSet) + "/" +
                                   AssetDatabase.AssetPathToGUID(expectedPayload) + ".bytes"));
                }
            }
            finally
            {
                CloseFixtureScenes(root);
                LightmapSettings.lightmaps = previousLightmaps;
                if (previous.IsValid() && previous.isLoaded)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(root);
            }
        }

        [Test]
        public void SharedTextures_PreserveReferencesAndImporterSettings_AcrossRebakes()
        {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var root = "Assets/MapBakeSyncCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
            try
            {
                var authoringPath = root + "/Authoring.unity";
                var compiledPath = root + "/Compiled.unity";
                foreach (var path in new[] { authoringPath, compiledPath })
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                    EditorSceneManager.CloseScene(scene, true);
                }

                AssetDatabase.CreateFolder(root, "Compiled");
                var payload = root + "/Compiled/Lightmap-0_comp_light.png";
                var texture = new Texture2D(2, 2);
                var bytes = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                File.WriteAllBytes(payload, bytes);
                AssetDatabase.ImportAsset(payload);
                var importer = (TextureImporter)AssetImporter.GetAtPath(payload);
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
                var guid = AssetDatabase.AssetPathToGUID(payload);

                var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
                // A material outside either scene's bake folder deliberately keeps the old generation alive.
                var serialized = new SerializedObject(material);
                var textures = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
                textures.arraySize = 1;
                textures.GetArrayElementAtIndex(0).FindPropertyRelative("first").stringValue = "_MainTex";
                textures.GetArrayElementAtIndex(0).FindPropertyRelative("second.m_Texture").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(payload);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var materialPath = root + "/External.mat";
                AssetDatabase.CreateAsset(material, materialPath);

                Assert.That(MapBakeArtifactFileMirror.CopyCompiledToAuthoring(compiledPath, authoringPath,
                    out var mirror, out var message), Is.True, message);
                var sharedPath = AssetDatabase.GUIDToAssetPath(guid);
                StringAssert.StartsWith(root + "/Generated/Bakes/", sharedPath);
                Assert.That(File.Exists(payload), Is.False);
                Assert.That(mirror.Relocated, Is.EqualTo(1));
                importer = (TextureImporter)AssetImporter.GetAtPath(sharedPath);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(importer.sRGBTexture, Is.False);
                CollectionAssert.Contains(AssetDatabase.GetDependencies(materialPath), sharedPath);

                // A rebake produces a fresh GUID under the original name. Never overwrite a shared asset.
                File.WriteAllBytes(payload, bytes);
                AssetDatabase.ImportAsset(payload);
                var nextGuid = AssetDatabase.AssetPathToGUID(payload);
                Assert.That(nextGuid, Is.Not.EqualTo(guid));
                Assert.That(MapBakeArtifactFileMirror.CopyCompiledToAuthoring(compiledPath, authoringPath,
                    out mirror, out message), Is.True, message);
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(sharedPath));
                Assert.That(AssetDatabase.GUIDToAssetPath(nextGuid), Is.Not.EqualTo(sharedPath));
                Assert.That(Directory.GetFiles(root + "/Generated/Bakes", "*.png", SearchOption.AllDirectories),
                    Has.Length.EqualTo(2));
                CollectionAssert.Contains(AssetDatabase.GetDependencies(materialPath), sharedPath);
            }
            finally
            {
                CloseFixtureScenes(root);
                if (previous.IsValid() && previous.isLoaded)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(root);
            }
        }

        private static string ProbePayloadPath(UnityEngine.Object bakingSet, bool scenario = false)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            object owner = bakingSet;
            if (scenario)
                owner = ((System.Collections.IDictionary)bakingSet.GetType().GetField("scenarios", flags)
                    .GetValue(bakingSet)).Values.Cast<object>().Single();
            var stream = owner.GetType().GetField(scenario ? "cellDataAsset" : "cellSharedDataAsset", flags)
                .GetValue(owner);
            return (string)stream.GetType().GetMethod("GetAssetPath").Invoke(stream, null);
        }

        private static void CloseFixtureScenes(string root)
        {
            for (var i = UnityEngine.SceneManagement.SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.path.StartsWith(root + "/", StringComparison.Ordinal))
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
