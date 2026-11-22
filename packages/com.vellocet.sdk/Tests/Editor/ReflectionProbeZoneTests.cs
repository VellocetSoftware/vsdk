using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Editor.Maps;
using Vertex.Contracts;
using Vertex.Unity.Editor;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class ReflectionProbeZoneTests
    {
        [TestCase(1d)]
        [TestCase(.0254d)]
        public void ZonesImportWithMetreBoundsAndRoundTripThroughSceneCapture(double metres)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = MapTestScenes.CreateEmpty();
            SceneManager.SetActiveScene(scene);
            var scenePath = "Assets/VertexReflectionRoundTrip_" + Guid.NewGuid().ToString("N") + ".unity";
            try
            {
                var catalog = VertexEntityCatalogExporter.Create();
                var definition = catalog.Entities.Single(entity => entity.Id == "env_reflection_probe");
                Assert.That(definition.Placement, Is.EqualTo("volume"));
                Assert.That(definition.Geometry, Is.EqualTo("bounds"));
                Assert.That(definition.Importable, Is.True);
                var map = Map(metres, new Vector3Data(8, 4, 2), new Vector3Data(0, 90, 0));
                using var built = VertexMapBuilder.Build(map, (_, _) => null);
                var marker = built.Root.GetComponentInChildren<ReflectionProbeMarker>(true);
                var probe = marker.GetComponent<ReflectionProbe>();
                Assert.That(probe.mode, Is.EqualTo(ReflectionProbeMode.Baked));
                Assert.That(probe.resolution, Is.EqualTo(64));
                Assert.That(probe.hdr && probe.boxProjection, Is.True);
                Assert.That(probe.intensity, Is.EqualTo(2));
                Assert.That(probe.blendDistance, Is.EqualTo(.25f));
                Assert.That(probe.importance, Is.EqualTo(3));
                Assert.That(Vector3.Distance(probe.bounds.size, new Vector3(2, 4, 8) * (float)metres), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(probe.bounds.center, new Vector3(3, 5, -7) * (float)metres), Is.LessThan(.0001f));
                Assert.That(marker.GetComponentsInChildren<Collider>().Length, Is.Zero);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                var captured = VertexSceneCapture.Capture(scene, new VertexSceneCapture.Options
                    { MapId = Guid.NewGuid(), Catalog = catalog,
                        GeneratedComponent = component => component is Vertex.Unity.VertexObjectIdentity ? "Vertex import identity" : null });
                Assert.That(captured.Complete, Is.True, string.Join("\n", captured.Entries
                    .Where(entry => entry.action == "unsupported").Select(entry => entry.component + ": " + entry.detail)));
                var entity = captured.Map.Containers.Single(node => node.Entity != null).Entity;
                Assert.That(entity.TypeId, Is.EqualTo("env_reflection_probe"));
                Assert.That(entity.Size.X, Is.EqualTo(8 * metres).Within(.0001));
                Assert.That(entity.Size.Y, Is.EqualTo(4 * metres).Within(.0001));
                Assert.That(entity.Size.Z, Is.EqualTo(2 * metres).Within(.0001));
                using var restored = VertexMapBuilder.Build(captured.Map, (_, _) => null);
                var restoredProbe = restored.Root.GetComponentInChildren<ReflectionProbe>();
                Assert.That(Vector3.Distance(restoredProbe.bounds.size, probe.bounds.size), Is.LessThan(.0001f));
                Assert.That(restoredProbe.resolution, Is.EqualTo(probe.resolution));
            }
            finally
            {
                MapTestScenes.Close(scene);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        public void DisabledAndInactiveZonesDoNotContributeReflections()
        {
            var map = Map(1, new Vector3Data(4, 4, 4), new Vector3Data());
            map.Containers[0].State = new MapObjectState { MarkerDisabled = true };
            using var disabled = VertexMapBuilder.Build(map, (_, _) => null);
            Assert.That(disabled.Root.GetComponentInChildren<ReflectionProbe>(true).enabled, Is.False);
            map.Containers[0].State = new MapObjectState { Inactive = true };
            using var inactive = VertexMapBuilder.Build(map, (_, _) => null);
            Assert.That(inactive.Root.GetComponentInChildren<ReflectionProbe>(true).gameObject.activeInHierarchy, Is.False);
        }

        [Test, Category("Graphics")]
        public void LightingBakeProducesPersistentCubemapAndBakeArtifactTransferRestoresIt()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Reflection cubemap baking needs a graphics-enabled Unity editor.");
            var previous = SceneManager.GetActiveScene();
            var scene = MapTestScenes.CreateEmpty();
            SceneManager.SetActiveScene(scene);
            var folder = "Assets/VertexReflectionZoneCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            Material material = null;
            try
            {
                using var built = VertexMapBuilder.Build(Map(1, new Vector3Data(8, 8, 8), new Vector3Data()), (_, _) => null);
                var probe = built.Root.GetComponentInChildren<ReflectionProbe>();
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = probe.transform.position + Vector3.forward * 3;
                cube.transform.localScale = Vector3.one * 2;
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", Color.red);
                cube.GetComponent<Renderer>().sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(cube, StaticEditorFlags.ReflectionProbeStatic);
                var settings = new LightingSettings { bakedGI = false, realtimeGI = false };
                AssetDatabase.CreateAsset(settings, folder + "/LightingSettings.asset");
                Lightmapping.lightingSettings = settings;
                RenderSettings.skybox = null;
                var path = folder + "/ReflectionZone.unity";
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                // The compiler uses this same Unity lighting bake; baked probes must work without a custom bake pass.
                Assert.That(Lightmapping.Bake(), Is.True);
                Assert.That(probe.bakedTexture, Is.Not.Null);
                var texturePath = AssetDatabase.GetAssetPath(probe.bakedTexture);
                Assert.That(texturePath.StartsWith(folder + "/", StringComparison.Ordinal), Is.True, texturePath);
                Assert.That(File.Exists(texturePath), Is.True);
                Assert.That(probe.bakedTexture.dimension, Is.EqualTo(TextureDimension.Cube));
                Assert.That(probe.bakedTexture.width, Is.EqualTo(64));
                var snapshot = MapCompiledBakeSnapshot.Capture(scene, new Dictionary<string, string>());
                probe.bakedTexture = null;
                var summary = snapshot.Apply(scene, MapCompiledBakeSnapshot.ApplyOptions.LightingOnly);
                Assert.That(summary.ReflectionProbesApplied, Is.EqualTo(1));
                Assert.That(AssetDatabase.GetAssetPath(probe.bakedTexture), Is.EqualTo(texturePath));
                // Remove the temporary capture object; its material is deliberately not a persistent scene asset.
                Object.DestroyImmediate(cube);
                AssetDatabase.SaveAssets();
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                MapTestScenes.Close(scene);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                probe = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>()).Single();
                Assert.That(AssetDatabase.GetAssetPath(probe.bakedTexture), Is.EqualTo(texturePath));
                Assert.That(probe.bakedTexture.width, Is.EqualTo(64));
            }
            finally
            {
                if (material) Object.DestroyImmediate(material);
                if (scene.IsValid() && scene.isLoaded) MapTestScenes.Close(scene);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static MapAsset Map(double metres, Vector3Data size, Vector3Data rotation) => new()
        {
            MapId = Guid.NewGuid(), MetersPerUnit = metres,
            Containers = new[] { new MapContainer { Id = Guid.NewGuid(), Name = "Room reflections", Entity = new MapEntity
            {
                SdkId = "com.vellocet.sdk", TypeId = "env_reflection_probe", Position = new Vector3Data(3, 5, 7),
                Size = size, Rotation = new QuaternionData(0, Math.Sin(rotation.Y * Math.PI / 360), 0, Math.Cos(rotation.Y * Math.PI / 360)), Properties = new[]
                {
                    new EntityPropertyValue { Id = "resolution", Value = 64 },
                    new EntityPropertyValue { Id = "intensity", Value = 2d },
                    new EntityPropertyValue { Id = "blend_distance", Value = .25d },
                    new EntityPropertyValue { Id = "importance", Value = 3 }
                }
            } } }
        };
    }
}
