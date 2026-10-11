// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Editor.Entities
{
    /// <summary>Scene-only previews from Editor/Avatars/{entity_class}.prefab, without spawning scene objects.</summary>
    [InitializeOnLoad]
    internal static class EntityAvatarPreview
    {
        internal const string AvatarRoot = Constants.RootPath + "/Editor/Avatars";
        private static readonly Dictionary<string, Avatar> Avatars = new();

        static EntityAvatarPreview()
        {
            EditorApplication.projectChanged += ClearCache;
        }

        internal static void ClearCache()
        {
            Avatars.Clear();
            SceneView.RepaintAll();
        }

        internal static void Draw(EntityMarker marker, GizmoType gizmoType)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !marker || !marker.isActiveAndEnabled ||
                !Camera.current || Camera.current.cameraType != CameraType.SceneView)
                return;

            var avatar = Resolve(marker.EntityClassName);
            if (avatar == null)
                return;

            var pose = GetWorldMatrix(marker.transform);
            foreach (var part in avatar.Parts)
            {
                if (!part.Mesh)
                    continue;
                for (var submesh = 0; submesh < part.Mesh.subMeshCount; submesh++)
                {
                    var material = part.Materials[Mathf.Min(submesh, part.Materials.Length - 1)];
                    if (material && material.SetPass(0))
                        Graphics.DrawMeshNow(part.Mesh, pose * part.LocalMatrix, submesh);
                }
            }

            if ((gizmoType & GizmoType.Selected) == 0)
                return;

            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = pose;
            Gizmos.color = SdkEditorStyle.SceneMarkerHalo;
            Gizmos.DrawWireCube(avatar.Bounds.center, avatar.Bounds.size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        internal static bool TryGetBounds(EntityMarker marker, out Bounds bounds)
        {
            bounds = default;
            if (!marker || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            var avatar = Resolve(marker.EntityClassName);
            if (avatar == null)
                return false;

            bounds = TransformBounds(avatar.Bounds, GetWorldMatrix(marker.transform));
            return true;
        }

        // Point entities are spawned at the marker's position/rotation, with their prefab's authored size.
        private static Matrix4x4 GetWorldMatrix(Transform marker)
        {
            return Matrix4x4.TRS(marker.position, marker.rotation, Vector3.one);
        }

        private static Avatar Resolve(string entityClass)
        {
            if (string.IsNullOrEmpty(entityClass))
                return null;
            if (Avatars.TryGetValue(entityClass, out var cached))
                return cached;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{AvatarRoot}/{entityClass}.prefab");
            var parts = new List<Part>();
            var bounds = default(Bounds);
            if (prefab)
            {
                var toAvatar = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix;
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
                {
                    if (!filter.sharedMesh || !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)
                        continue;
                    var materials = renderer.sharedMaterials;
                    if (materials.Length == 0)
                        continue;

                    var matrix = toAvatar * filter.transform.localToWorldMatrix;
                    var partBounds = TransformBounds(filter.sharedMesh.bounds, matrix);
                    if (parts.Count == 0)
                        bounds = partBounds;
                    else
                        bounds.Encapsulate(partBounds);
                    parts.Add(new Part(filter.sharedMesh, matrix, materials));
                }
            }

            var avatar = parts.Count > 0 ? new Avatar(parts.ToArray(), bounds) : null;
            Avatars[entityClass] = avatar;
            return avatar;
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (var corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                result.Encapsulate(matrix.MultiplyPoint3x4(point));
            }
            return result;
        }

        private sealed class Avatar
        {
            internal readonly Part[] Parts;
            internal readonly Bounds Bounds;

            internal Avatar(Part[] parts, Bounds bounds)
            {
                Parts = parts;
                Bounds = bounds;
            }
        }

        private readonly struct Part
        {
            internal readonly Mesh Mesh;
            internal readonly Matrix4x4 LocalMatrix;
            internal readonly Material[] Materials;

            internal Part(Mesh mesh, Matrix4x4 localMatrix, Material[] materials)
            {
                Mesh = mesh;
                LocalMatrix = localMatrix;
                Materials = materials;
            }
        }
    }
}
