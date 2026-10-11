// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Entities;

namespace Vellocet.Sdk.Tests
{
    public sealed class EntityAvatarPreviewTests
    {
        [Test]
        public void ResupplyAvatarIsSelfContainedAndHasNoRuntimeComponents()
        {
            var path = EntityAvatarPreview.AvatarRoot + "/func_resupply.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.GetComponentsInChildren<MeshFilter>().Length, Is.GreaterThan(0));
            Assert.That(prefab.GetComponentsInChildren<Component>().All(component =>
                component is Transform or MeshFilter or MeshRenderer), Is.True,
                "Avatars contain visual geometry only: no colliders, scripts, audio or network objects.");
            foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                Assert.That(dependency.StartsWith(EntityAvatarPreview.AvatarRoot + "/") ||
                            !dependency.StartsWith("Assets/"), Is.True,
                    $"The standalone SDK cannot depend on base-game asset {dependency}.");
        }

        [Test]
        public void AvatarBoundsFollowPositionAndRotationWithoutChangingTheMarker()
        {
            var parent = new GameObject("Avatar bounds parent");
            var gameObject = new GameObject("Avatar bounds marker");
            gameObject.transform.SetParent(parent.transform);
            try
            {
                var marker = gameObject.AddComponent<ResupplyMarker>();
                Assert.That(EntityAvatarPreview.TryGetBounds(marker, out var original), Is.True);
                Assert.That(original.size.x, Is.InRange(2f, 2.3f));
                Assert.That(original.size.y, Is.InRange(3f, 3.5f));
                Assert.That(original.size.z, Is.InRange(1f, 1.5f), "The avatar must show the closed doors.");
                var children = gameObject.transform.childCount;
                var components = gameObject.GetComponents<Component>().Length;
                var id = marker.Guid;
                parent.transform.localScale = new Vector3(3f, 2f, 4f);
                marker.transform.localScale = new Vector3(2f, 3f, 2f);
                marker.transform.SetPositionAndRotation(new Vector3(10f, 4f, -6f), Quaternion.Euler(0f, 90f, 0f));
                Assert.That(EntityAvatarPreview.TryGetBounds(marker, out var moved), Is.True);
                Assert.That(Vector3.Distance(moved.center, marker.transform.position + marker.transform.rotation * original.center),
                    Is.LessThan(0.001f));
                Assert.That(moved.size.x, Is.EqualTo(original.size.z).Within(0.001f));
                Assert.That(moved.size.z, Is.EqualTo(original.size.x).Within(0.001f));
                Assert.That(moved.size.y, Is.EqualTo(original.size.y).Within(0.001f));
                Assert.That(marker.transform.childCount, Is.EqualTo(children));
                Assert.That(gameObject.GetComponents<Component>().Length, Is.EqualTo(components));
                Assert.That(marker.Guid, Is.EqualTo(id));

                var editor = UnityEditor.Editor.CreateEditor(marker);
                try
                {
                    Assert.That(editor, Is.TypeOf<EntityMarkerEditor>());
                    Assert.That(((EntityMarkerEditor)editor).HasFrameBounds(), Is.True);
                    Assert.That(((EntityMarkerEditor)editor).OnGetFrameBounds(), Is.EqualTo(moved));
                }
                finally
                {
                    Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void MarkersWithoutAvatarsKeepTheirExistingGizmos()
        {
            var gameObject = new GameObject("Unmapped avatar marker");
            try
            {
                var marker = gameObject.AddComponent<PickupMarker>();
                Assert.That(EntityAvatarPreview.TryGetBounds(marker, out _), Is.False);
                Assert.That(marker.IconPath, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
