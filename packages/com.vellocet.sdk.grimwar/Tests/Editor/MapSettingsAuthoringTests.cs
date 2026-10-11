using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Maps;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class MapSettingsAuthoringTests
    {
        [TestCase(MapMusicSet.Unassigned, MapMusicSet.Ordainment)]
        [TestCase(MapMusicSet.Compound, MapMusicSet.Compound)]
        [TestCase(MapMusicSet.Ritual, MapMusicSet.Ritual)]
        [TestCase(MapMusicSet.Arcade, MapMusicSet.Arcade)]
        [TestCase(MapMusicSet.Ordainment, MapMusicSet.Ordainment)]
        public void CustomMap_UsesTheSdkChoiceThroughCatalogExportAndSceneImport(MapMusicSet choice, MapMusicSet expected)
        {
            var catalog = EntityCatalog.Deserialize(VertexEntityCatalogExporter.Create().Serialize());
            var definition = catalog.Entities.Single(entity => entity.Id == MapSettingsMarker.EntityId);
            var property = definition.Properties.Single(field => field.Id == "music_set");
            Assert.That(property.Type, Is.EqualTo("enum"));
            Assert.That(property.Choices.Any(value => value.Name == choice.ToString()), Is.True);
            var map = MapAsset.Deserialize(Map(choice).Serialize());
            map.Containers[0].Entity.Validate(catalog);
            Assert.That(MapSettingsAuthoring.Read(map), Is.EqualTo(expected));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                using var built = VertexMapBuilder.Build(map, null);
                SceneManager.MoveGameObjectToScene(built.Root, scene);
                Assert.That(MapSettingsAuthoring.Read(scene).MusicSet, Is.EqualTo(choice));
                var metadata = ScriptableObject.CreateInstance<MapDefinition>();
                try
                {
                    metadata.id = "custom_map";
                    metadata.musicSet = MapSettingsAuthoring.Compile(scene);
                    Assert.That(metadata.musicSet, Is.EqualTo(expected));
                    Assert.That(built.Root.GetComponentsInChildren<MapSettingsMarker>(true), Is.Empty);
                    Assert.That(MapMusicSets.Name(metadata.musicSet), Is.EqualTo(expected.ToString()));
                    metadata.id = "gr_ritual";
                    Assert.That(MapMusicSets.Name(metadata.musicSet), Is.EqualTo(expected.ToString()));
                }
                finally { UnityEngine.Object.DestroyImmediate(metadata); }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CustomMap_CompilesWithDefaultMusicWithoutSettingsOrMusicOverride(bool hasSettings)
        {
            var map = Map(MapMusicSet.Unassigned);
            if (hasSettings) map.Containers[0].Entity.Properties = Array.Empty<EntityPropertyValue>();
            else map.Containers = Array.Empty<MapContainer>();
            map = MapAsset.Deserialize(map.Serialize());
            Assert.That(MapSettingsAuthoring.Read(map), Is.EqualTo(MapMusicSet.Ordainment));

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                using var built = VertexMapBuilder.Build(map, null);
                SceneManager.MoveGameObjectToScene(built.Root, scene);
                Assert.That(MapSettingsAuthoring.Compile(scene), Is.EqualTo(MapMusicSet.Ordainment));
                Assert.That(built.Root.GetComponentsInChildren<MapSettingsMarker>(true), Is.Empty);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void Compile_ExtractsSettingsFromPrefabWithoutChangingTheSource()
        {
            var path = "Assets/MapMusicSettingsTest_" + Guid.NewGuid().ToString("N") + ".prefab";
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                using var built = VertexMapBuilder.Build(Map(MapMusicSet.Compound), null);
                var prefab = PrefabUtility.SaveAsPrefabAsset(built.Root, path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Assert.That(MapSettingsAuthoring.Compile(scene), Is.EqualTo(MapMusicSet.Compound));
                Assert.That(instance.GetComponentsInChildren<MapSettingsMarker>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<MapSettingsMarker>(true), Has.Length.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void Authoring_RejectsDuplicateSettingsAndInvalidOverrides()
        {
            var map = Map(MapMusicSet.Compound);
            map.Containers = new[] { Map(MapMusicSet.Compound).Containers[0], Map(MapMusicSet.Ritual).Containers[0] };
            Assert.Throws<InvalidDataException>(() => MapSettingsAuthoring.Read(map));
            foreach (var invalid in new object[] { -1, 99, 1.5, double.NaN, double.PositiveInfinity, "Compound", true })
            {
                map = Map(MapMusicSet.Compound);
                map.Containers[0].Entity.Properties[0].Value = invalid;
                Assert.Throws<InvalidDataException>(() => MapSettingsAuthoring.Read(map), invalid.ToString());
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("Unassigned")]
        public void RuntimeMetadata_UsesDefaultForMissingOrUnassignedMusic(string value)
        {
            Assert.That(MapMusicSets.TryParse(value, out var musicSet), Is.True);
            Assert.That(musicSet, Is.EqualTo(MapMusicSet.Ordainment));
            Assert.That(MapMusicSets.Parse(value), Is.EqualTo(MapMusicSet.Ordainment));
            Assert.That(MapMusicSets.IsValid(default), Is.True);
            Assert.That(MapMusicSets.Name(default), Is.EqualTo("Ordainment"));
        }

        [TestCase("0")]
        [TestCase("1")]
        [TestCase("compound")]
        [TestCase("UnknownMusicSet")]
        public void RuntimeMetadata_RejectsUnknownMusicOverrides(string value) =>
            Assert.Throws<InvalidDataException>(() => MapMusicSets.Parse(value));

        private static MapAsset Map(MapMusicSet choice) => new()
        {
            MapId = Guid.NewGuid(), MetersPerUnit = .0254, Containers = new[]
            {
                new MapContainer
                {
                    Id = Guid.NewGuid(), Name = "Map Settings", Entity = new MapEntity
                    {
                        SdkId = Constants.PackageId, TypeId = MapSettingsMarker.EntityId,
                        Size = new Vector3Data(16, 16, 16),
                        Properties = new[] { new EntityPropertyValue { Id = "music_set", Value = (int)choice } }
                    }
                }
            }
        };
    }
}
