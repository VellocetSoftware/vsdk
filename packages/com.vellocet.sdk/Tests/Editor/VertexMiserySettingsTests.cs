#if VELLOCET_STEREOFORM
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Entities;
using Vellocet.Stereoform;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class VertexMiserySettingsTests
    {
        [Test]
        public void GroupAudioKeepsItsEmitterBindingsAndDoesNotAddAudioToOrdinaryGroups()
        {
            var catalog = VertexEntityCatalogExporter.Create();
            var audio = ScriptableObject.CreateInstance<AudioEventReference>();
            var bindings = ScriptableObject.CreateInstance<AudioEventBindings>();
            try
            {
                var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 1, Containers = new[]
                {
                    Node("With audio", "func_surface", catalog.SdkId,
                        Value("audio_emitter", true), Value("audio_event", "asset:test/event"), Value("audio_bindings", "asset:test/bindings"),
                        Value("audio_auto_play", false), Value("audio_loop", true), Value("audio_occlusion", false), Value("audio_enabled", false)),
                    Node("Without audio", "func_surface", catalog.SdkId)
                } };
                using var result = VertexMapBuilder.Build(MapAsset.Deserialize(map.Serialize()), (_, _) => null, resolveAsset: key => key.EndsWith("/event") ? audio : bindings);
                var emitter = result.Root.GetComponentsInChildren<AudioEmitter>(true).Single();
                Assert.That(emitter.GetComponent<Vertex.Unity.VertexObjectIdentity>().ObjectId, Is.EqualTo(map.Containers[0].Id));
                Assert.That(emitter.Event, Is.SameAs(audio)); Assert.That(emitter.Bindings, Is.SameAs(bindings));
                Assert.That(emitter.AutoPlay, Is.False); Assert.That(emitter.Loop, Is.True);
                Assert.That(emitter.SpatialOcclusion, Is.False); Assert.That(emitter.enabled, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(audio); UnityEngine.Object.DestroyImmediate(bindings); }
        }

        [Test]
        public void LightKeepsCustomUrpLayersAndShadowResolution()
        {
            var catalog = VertexEntityCatalogExporter.Create();
            var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 1, Containers = new[]
            {
                Node("Custom light", "env_light", catalog.SdkId, Value("shadow_resolution_tier", -1), Value("shadow_resolution", 2048),
                    Value("shadow_custom_resolution", 1024), Value("soft_shadow_quality", 3), Value("custom_shadow_layers", true),
                    Value("urp_rendering_layers", 4), Value("shadow_rendering_layers", 2))
            } };
            using var result = VertexMapBuilder.Build(MapAsset.Deserialize(map.Serialize()), (_, _) => null);
            var light = result.Root.GetComponentInChildren<Light>(true);
            var data = light.GetUniversalAdditionalLightData();
            Assert.That(data.additionalLightsShadowResolutionTier, Is.EqualTo(-1));
            Assert.That((int)light.shadowResolution, Is.EqualTo(2048)); Assert.That(light.shadowCustomResolution, Is.EqualTo(1024));
            Assert.That(data.softShadowQuality, Is.EqualTo(SoftShadowQuality.High));
            Assert.That(data.customShadowLayers, Is.True);
            Assert.That((uint)data.renderingLayers, Is.EqualTo(4)); Assert.That((uint)data.shadowRenderingLayers, Is.EqualTo(2));
            var fields = EntityCatalog.Deserialize(catalog.Serialize()).Entities.Single(entity => entity.Id == "env_light").Properties;
            foreach (var id in new[] { "use_pipeline_bias", "shadow_resolution_tier", "shadow_resolution", "shadow_custom_resolution",
                         "soft_shadow_quality", "custom_shadow_layers", "urp_rendering_layers", "shadow_rendering_layers",
                         "shadow_bias", "shadow_normal_bias", "shadow_near_plane", "culling_mask", "rendering_layer_mask" })
                Assert.That(fields.Single(field => field.Id == id).Hidden, Is.True, id);
            foreach (var id in new[] { "light_type", "color", "intensity", "range", "spot_angle", "shadows" })
                Assert.That(fields.Single(field => field.Id == id).Hidden, Is.False, id);
            foreach (var enabled in new[] { false, true })
            {
                map.Containers[0].Entity.Properties = new[] { Value("shadow_mode", enabled ? 0 : 2), Value("shadows", enabled) };
                using var toggled = VertexMapBuilder.Build(map, (_, _) => null);
                Assert.That(toggled.Root.GetComponentInChildren<Light>(true).shadows,
                    Is.EqualTo(enabled ? LightShadows.Soft : LightShadows.None), "Cast shadows overrides the imported mode.");
            }
        }

        [Test]
        public void EditableCaptureKeepsVertexAlphaUsedByCableMaterials()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            try
            {
                mesh.colors = Enumerable.Range(0, mesh.vertexCount).Select(i => new Color(1, 1, 1, i / (float)mesh.vertexCount)).ToArray();
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var captured = VertexSceneCapture.Mesh(mesh, go.transform, go.GetComponent<MeshRenderer>(), _ => "builtin:checker");
                captured.Id = Guid.NewGuid(); captured.Name = "Captured cable material"; captured.Lightmap.GenerateUV2 = false;
                var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 1, Objects = new[] { captured } };
                using var result = VertexMapBuilder.Build(MapAsset.Deserialize(map.Serialize()), (_, _) => go.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.That(result.Root.GetComponentInChildren<MeshFilter>(true).sharedMesh.colors.Select(c => c.a),
                    Is.EqualTo(mesh.colors.Select(c => c.a)));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void PointLightCaptureIgnoresFlattenedTransformScale()
        {
            var go = new GameObject("Flattened light");
            try
            {
                go.transform.localScale = new Vector3(0, 1, 1);
                var entity = VertexSceneCapture.RawEntity("env_light", go.transform, VertexEntityCatalogExporter.Create());
                Assert.DoesNotThrow(() => entity.Validate());
                Assert.That(entity.Size.X, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static MapContainer Node(string name, string type, string sdk, params EntityPropertyValue[] properties) => new()
        { Id = Guid.NewGuid(), Name = name, Entity = new MapEntity { SdkId = sdk, TypeId = type, Properties = properties } };
        private static EntityPropertyValue Value(string id, object value) => new() { Id = id, Value = value };
    }
}
#endif
