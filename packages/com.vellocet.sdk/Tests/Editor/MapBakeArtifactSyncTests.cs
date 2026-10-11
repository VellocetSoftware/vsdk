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

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var root = "Assets/MapBakeSyncCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
            try
            {
                var compiledPath = root + "/Compiled.unity";
                var authoringPath = root + "/Authoring.unity";
                foreach (var path in new[] { authoringPath, compiledPath })
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                }

                var compiled = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
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
                serialized.FindProperty("settings.virtualOffsetSettings.searchMultiplier").floatValue = 0.35f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(compiled);
                AssetDatabase.SaveAssets();

                // Forward after a bake, then back into a compile that preserves existing lighting.
                foreach (var targetPath in new[] { authoringPath, compiledPath })
                {
                    var source = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    MapBakeArtifactFileMirror.Result mirror;
                    var mirrored = targetPath == authoringPath
                        ? MapBakeArtifactFileMirror.CopyCompiledToAuthoring(source.path, targetPath, out mirror, out message)
                        : MapBakeArtifactFileMirror.CopyAuthoringToCompiled(source.path, targetPath, out mirror, out message);
                    Assert.That(mirrored, Is.True, message);
                    var snapshot = MapCompiledBakeSnapshot.Capture(source, mirror.AssetMap);
                    var target = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);
                    snapshot.Apply(target, MapCompiledBakeSnapshot.ApplyOptions.LightingOnly);
                    snapshot.Apply(target, MapCompiledBakeSnapshot.ApplyOptions.LightingOnly);
                    Assert.That(EditorSceneManager.SaveScene(target), Is.True);
                    AssetDatabase.SaveAssets();
                    target = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);

                    var name = Path.GetFileNameWithoutExtension(targetPath);
                    var expectedSet = root + "/" + name + "/" + name + " Baking Set.asset";
                    var expectedPayload = root + "/" + name + "/" + name + " Baking Set.CellSharedData.bytes";
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
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(root);
                if (setup.Any(s => s.isActive && s.isLoaded))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
#endif
