// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using Vertex.VSig;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.VSig;
using Vellocet.Sdk.VSig.Runtime;
using Object = UnityEngine.Object;
using VSigCompiledProgram = Vertex.VSig.VSigCompiledProgram;

namespace Vellocet.Sdk.Editor.VSig
{
    [InitializeOnLoad]
    internal static class VSigPlayModeBootstrap
    {
        static VSigPlayModeBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    SceneManager.sceneLoaded += OnSceneLoaded;
                    InstallForLoadedScenes();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    SceneManager.sceneLoaded -= OnSceneLoaded;
                    break;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForScene(scene);
        }

        private static void InstallForLoadedScenes()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                InstallForScene(SceneManager.GetSceneAt(i));
        }

        private static void InstallForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            // Bundled scenes already carry compiled VSig; their build-machine paths are not local authoring sources.
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path))
                return;

            if (!VSigMapSourceUtility.TryFindForScene(scene.path, out var source, out var sourcePath))
                return;

            var markers = CollectMarkers(scene);
            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source.text, diagnostics, markers, sourcePath);
            VSigSceneCompiler.Validate(program, markers, diagnostics);
            if (diagnostics.Exists(diagnostic => diagnostic.Severity == VSigDiagnosticSeverity.Error))
            {
                foreach (var diagnostic in diagnostics)
                    if (diagnostic.Severity == VSigDiagnosticSeverity.Error)
                        Debug.LogError($"[VSig] {diagnostic}");
                return;
            }

            Guid.TryParse(AssetDatabase.AssetPathToGUID(scene.path), out var sceneId);
            try
            {
                VSigSceneInstaller.Install(scene, VSigCompiledProgram.FromProgram(program),
                    mapId: VSigSceneCompiler.ResolveMapId(scene, sceneId));
            }
            catch (ArgumentException error) { Debug.LogError("[VSig] " + error.Message); }
        }

        private static EntityMarker[] CollectMarkers(Scene scene)
        {
            var markers = Object.FindObjectsByType<EntityMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return Array.FindAll(markers,
                marker => marker && marker.gameObject.scene.handle == scene.handle);
        }
    }
}
#endif
