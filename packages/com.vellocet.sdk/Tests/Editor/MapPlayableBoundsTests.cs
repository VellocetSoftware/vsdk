#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Tests
{
    public sealed class MapPlayableBoundsTests
    {
        private Scene _scene;
        [SetUp] public void SetUp() => _scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(_scene);
        private GameObject Object(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, _scene);
            return go;
        }
        private MapRuntimeDependencies Baked() => _scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MapRuntimeDependencies>()).Single();

        [Test]
        public void EmptyMapHasNoFallbackBoxAndStaticGeometryIsBaked()
        {
            MapPlayableBoundsBaker.Bake(_scene);
            Assert.That(Baked().TryGetPlayableBounds(out _), Is.False);
            var geometry = Object("geometry");
            geometry.transform.position = new Vector3(100, 2, -50);
            geometry.AddComponent<BoxCollider>().size = new Vector3(40, 4, 60);
            var trigger = Object("ignored trigger");
            trigger.transform.position = Vector3.one * 10000;
            trigger.AddComponent<BoxCollider>().isTrigger = true;
            MapPlayableBoundsBaker.Bake(_scene);
            Assert.That(Baked().TryGetPlayableBounds(out var bounds), Is.True);
            Assert.That(bounds.center, Is.EqualTo(geometry.transform.position));
            Assert.That(bounds.size, Is.EqualTo(new Vector3(40, 4, 60)));
        }

        [Test]
        public void AuthoredOverrideIsCenteredOnTheMapSettingsEntity()
        {
            var settings = Object("settings").AddComponent<MapSettingsMarker>();
            settings.transform.position = new Vector3(400, 10, -200);
            var data = new SerializedObject(settings);
            data.FindProperty("overridePlayableBounds").boolValue = true;
            data.FindProperty("playableBoundsSize").vector3Value = new Vector3(800, 200, 600);
            data.ApplyModifiedPropertiesWithoutUndo();
            MapPlayableBoundsBaker.Bake(_scene);
            Assert.That(Baked().TryGetPlayableBounds(out var bounds), Is.True);
            Assert.That(bounds, Is.EqualTo(new Bounds(settings.transform.position, new Vector3(800, 200, 600))));
            data.FindProperty("playableBoundsSize").vector3Value = Vector3.zero;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentException>(() => MapPlayableBoundsBaker.Bake(_scene));
        }
    }
}
#endif
