using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Splines;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Editor.Entities;
using Vertex.Contracts;
using Vertex.Unity;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class VertexCapturedSettingsTests
    {
        [Test]
        public void NativeBoundsKeepColliderOffsetsAndChildWorldPose()
        {
            var catalog = VertexEntityCatalogExporter.Create();
            var owner = Guid.NewGuid(); var child = Guid.NewGuid();
            var entity = new MapEntity { SdkId = catalog.SdkId, TypeId = EntityMarker.GetEntityClassName(typeof(TriggerHurtMarker)),
                Position = new Vector3Data(2, 8, 5), Size = new Vector3Data(4, 6, 2), Properties = new[]
                {
                    Value("volume_shape", "box"), Value("volume_center", new object[] { .1d, .2d, .3d }),
                    Value("volume_size", new object[] { 3d, 4d, 5d }), Value("volume_is_trigger", true),
                    Value("volume_layer", "Default"), Value("volume_enabled", false)
                } };
            var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 1, Containers = new[]
            {
                new MapContainer { Id = owner, Name = "Volume", Entity = entity },
                new MapContainer { Id = child, Name = "Authored helper", ParentId = owner, Position = new Vector3Data(10, 20, 30) }
            } };
            using var result = VertexMapBuilder.Build(map, (_, _) => null);
            var marker = result.Root.GetComponentInChildren<TriggerHurtMarker>(true);
            var shape = marker.GetComponent<BoxCollider>();
            Assert.That(shape.center, Is.EqualTo(new Vector3(.1f, .2f, -.3f)));
            Assert.That(shape.size, Is.EqualTo(new Vector3(3, 4, 5)));
            Assert.That(shape.enabled, Is.False);
            var helper = result.Root.GetComponentsInChildren<VertexObjectIdentity>(true).Single(i => i.ObjectId == child);
            Assert.That(Vector3.Distance(helper.transform.position, new Vector3(10, 20, -30)), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(helper.transform.lossyScale, Vector3.one), Is.LessThan(.0001f));
        }

        [Test]
        public void DiscLightsKeepNativeShadowSettingsAndZeroRange()
        {
            var catalog = VertexEntityCatalogExporter.Create();
            var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 1, Containers = new[]
            {
                new MapContainer { Id = Guid.NewGuid(), Name = "Disc", State = new MapObjectState { Inactive = true },
                    Entity = new MapEntity { SdkId = catalog.SdkId, TypeId = "env_light", Properties = new[]
                    {
                        Value("light_type", (int)LightType.Disc), Value("range", 0d), Value("area_size", new object[] { 3d, 2d }),
                        Value("shadow_mode", (int)LightShadows.Hard), Value("shadow_strength", .4d), Value("use_pipeline_bias", false)
                    } } }
            } };
            using var result = VertexMapBuilder.Build(map, (_, _) => null);
            var light = result.Root.GetComponentInChildren<Light>(true);
            Assert.That(light.type, Is.EqualTo(LightType.Disc));
            Assert.That(light.lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked));
            Assert.That(light.range, Is.Zero);
            Assert.That(light.shadows, Is.EqualTo(LightShadows.Hard));
            Assert.That(light.shadowStrength, Is.EqualTo(.4f));
            Assert.That(light.gameObject.activeSelf, Is.False);
        }

        private static EntityPropertyValue Value(string id, object value) => new() { Id = id, Value = value };

        [Test]
        public void CapturedAutomaticSplineKeepsItsAuthoredCurve()
        {
            object Pair(string name, object value) => new object[] { name, value };
            object Knot(double x, double tangent) => new object[]
            {
                Pair("position", new object[] { x, 0d, 0d }),
                Pair("tangent_in", new object[] { 0d, 0d, -tangent }),
                Pair("tangent_out", new object[] { 0d, 0d, tangent }),
                Pair("rotation", new object[] { 0d, 0d, 0d, 1d }),
                Pair("mode", (int)TangentMode.AutoSmooth), Pair("tension", .15d)
            };
            var node = new MapContainer { Entity = new MapEntity { Properties = new[]
            {
                Value("path_splines", new object[] { new object[] { Pair("closed", false),
                    Pair("knots", new object[] { Knot(0, 1.4), Knot(2, .8) }), Pair("speed_mult", 2d), Pair("stamina_mult", .5d) } })
            } } };
            var go = new GameObject("Captured spline");
            try
            {
                var target = go.AddComponent<SplineContainer>();
                typeof(VertexEntityCatalogExporter).Assembly.GetType("Vellocet.Sdk.Editor.Entities.VertexEntityProperties")
                    .GetMethod("RestoreSplines", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { target, node, Array.Empty<Vector3Data>() });
                Assert.That(target.Spline.GetTangentMode(0), Is.EqualTo(TangentMode.AutoSmooth));
                Assert.That(target.Spline[0].TangentOut.z, Is.EqualTo(-1.4f).Within(.00001f));
                Assert.That(target.Spline[1].TangentOut.z, Is.EqualTo(-.8f).Within(.00001f));
                Assert.That(target.Spline.GetOrCreateFloatData("speedMult").DefaultValue, Is.EqualTo(2));
                Assert.That(target.Spline.GetOrCreateFloatData("staminaMult").DefaultValue, Is.EqualTo(.5f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
