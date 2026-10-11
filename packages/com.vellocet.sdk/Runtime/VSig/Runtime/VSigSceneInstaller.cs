// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vellocet.Sdk.VSig.Runtime
{
    public static class VSigSceneInstaller
    {
        private const string RuntimeObjectName = "[VSig Runtime]";

        public static bool Install(Scene scene, VSigCompiledProgram program, bool includeInactiveMarkers = true,
            HideFlags hideFlags = HideFlags.DontSave, Guid mapId = default)
        {
            if (program is not { HasContent: true })
                return false;

            var runtime = FindOrCreateRuntime(scene, hideFlags);
            if (!runtime)
                return false;

            runtime.Configure(program, includeInactiveMarkers, mapId);
            return runtime.CompileAndRegister();
        }

        public static bool Install(Scene scene, Vertex.VSig.VSigCompiledProgram program, Guid mapId,
            bool includeInactiveMarkers = true, HideFlags hideFlags = HideFlags.DontSave)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (mapId == Guid.Empty) throw new ArgumentException("Vertex maps require a persistent map ID.", nameof(mapId));
            var runtime = FindOrCreateRuntime(scene, hideFlags);
            if (!runtime) return false;
            runtime.Configure(program, mapId, includeInactiveMarkers);
            return runtime.CompileAndRegister();
        }

        private static VSigSceneRuntime FindOrCreateRuntime(Scene scene, HideFlags hideFlags)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (!root)
                    continue;

                var existing = root.GetComponentInChildren<VSigSceneRuntime>(true);
                if (existing)
                {
                    existing.gameObject.hideFlags = hideFlags;
                    return existing;
                }
            }

            var host = new GameObject(RuntimeObjectName)
            {
                hideFlags = hideFlags
            };
            SceneManager.MoveGameObjectToScene(host, scene);
            return host.AddComponent<VSigSceneRuntime>();
        }
    }
}
