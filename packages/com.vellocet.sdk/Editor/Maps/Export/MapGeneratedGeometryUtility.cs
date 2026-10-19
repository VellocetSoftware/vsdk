// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapGeneratedGeometryUtility
    {
        internal static bool Validate(Scene scene, out string message)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                message = "The map scene is not loaded.";
                return false;
            }

            foreach (var transform in MapSceneTraversalUtility.GetComponents<Transform>(scene))
                if (PrefabUtility.IsPrefabAssetMissing(transform.gameObject))
                {
                    message = $"Missing map prefab on '{transform.name}'. Reimport its source asset before compiling.";
                    return false;
                }

            foreach (var filter in MapSceneTraversalUtility.GetComponents<MeshFilter>(scene))
                if (!filter.sharedMesh && filter.GetComponent<MeshRenderer>() &&
                    (GameObjectUtility.AreStaticEditorFlagsSet(filter.gameObject, StaticEditorFlags.ContributeGI) ||
                     filter.GetComponent<Vertex.Unity.VertexObjectIdentity>()))
                {
                    message = $"Missing world mesh on '{filter.name}'. Reimport its source asset before compiling.";
                    return false;
                }

            message = "Map prefab and world mesh references are valid.";
            return true;
        }

        internal static bool Persist(Scene scene, string compiledScenePath, out string message)
        {
            if (!Validate(scene, out message)) return false;
            var filters = MapSceneTraversalUtility.GetComponents<MeshFilter>(scene);
            var colliders = MapSceneTraversalUtility.GetComponents<MeshCollider>(scene);
            var skins = MapSceneTraversalUtility.GetComponents<SkinnedMeshRenderer>(scene);
            var meshes = filters.Select(f => f.sharedMesh).Concat(colliders.Select(c => c.sharedMesh))
                .Concat(skins.Select(s => s.sharedMesh)).Where(IsVertexMesh).Distinct().ToArray();
            if (meshes.Length == 0)
            {
                message = "No source Vertex meshes need copying.";
                return true;
            }

            var folder = MapEditorPathUtility.GetSceneDataFolderAssetPath(compiledScenePath);
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidDataException("Compiled geometry needs a saved scene under Assets.");
            var path = folder + "/VertexGeometry.asset";
            if (File.Exists(path))
                throw new InvalidDataException("Compiled geometry already exists at " + path + ". Compile into a fresh output folder.");
            if (!MapEditorPathUtility.EnsureAssetFolder(folder))
                throw new InvalidDataException("Could not create compiled geometry folder: " + folder);

            // The scene must own these meshes. Retaining importer subassets lets a failed
            // source reimport erase the world from an otherwise valid baked scene.
            var copies = new Dictionary<Mesh, Mesh>();
            Mesh main = null;
            try
            {
                foreach (var mesh in meshes)
                {
                    var copy = Object.Instantiate(mesh);
                    copy.name = mesh.name;
                    copy.hideFlags = HideFlags.None;
                    if (!main) { AssetDatabase.CreateAsset(copy, path); main = copy; }
                    else AssetDatabase.AddObjectToAsset(copy, main);
                    copies.Add(mesh, copy);
                }
                AssetDatabase.SaveAssetIfDirty(main);
            }
            catch
            {
                if (main) AssetDatabase.DeleteAsset(path);
                throw;
            }

            var references = 0;
            foreach (var filter in filters)
                if (filter.sharedMesh && copies.TryGetValue(filter.sharedMesh, out var mesh))
                { filter.sharedMesh = mesh; references++; }
            foreach (var collider in colliders)
                if (collider.sharedMesh && copies.TryGetValue(collider.sharedMesh, out var mesh))
                { collider.sharedMesh = mesh; references++; }
            foreach (var skin in skins)
                if (skin.sharedMesh && copies.TryGetValue(skin.sharedMesh, out var mesh))
                { skin.sharedMesh = mesh; references++; }
            EditorSceneManager.MarkSceneDirty(scene);
            message = $"Stored {copies.Count} Vertex mesh(es) with the compiled scene and replaced {references} render/collision reference(s).";
            return true;
        }

        private static bool IsVertexMesh(Mesh mesh) => mesh &&
            AssetDatabase.GetAssetPath(mesh).EndsWith(".vertexmesh", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
