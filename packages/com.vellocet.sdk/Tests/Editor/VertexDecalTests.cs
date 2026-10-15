using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Editor.Entities;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class VertexDecalTests
    {
        [Test]
        public void CatalogFiltersMaterialsByNativeProjectorRole()
        {
            var catalog = VertexEntityCatalogExporter.Create();
            var definition = catalog.Entities.Single(entity => entity.Id == "env_decal");
            Assert.That(definition.DecalMaterialProperty, Is.EqualTo("material"));
            Assert.That(definition.DecalOpacityProperty, Is.EqualTo("opacity"));
            var field = definition.Properties.Single(property => property.Id == "material");
            Assert.That(field.AssetRole, Is.EqualTo("decal"));
            Assert.That(field.AcceptsAsset(new CatalogAsset { Kind = "material", Role = "decal" }), Is.True);
            Assert.That(field.AcceptsAsset(new CatalogAsset { Kind = "material" }), Is.False);
        }

        [Test]
        public void NativeImportPreservesProjectorMaterialPoseSizeOpacityAndDistance()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Decal.shadergraph");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(VertexAssetCatalogExporter.IsDecalMaterial(material), Is.True);
                var catalog = VertexEntityCatalogExporter.Create();
                var id = Guid.NewGuid();
                var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = .25, Containers = new[] {
                    new MapContainer { Id = id, Name = "Wall decal", Entity = new MapEntity {
                        SdkId = catalog.SdkId, TypeId = "env_decal", Position = new Vector3Data(8, 12, 20),
                        Size = new Vector3Data(8, 4, 2), Rotation = new QuaternionData(0, 0, Math.Sqrt(.5), Math.Sqrt(.5)),
                        Properties = new[] { Value("material", "asset:fixture/decal"), Value("opacity", .4d), Value("draw_distance", 75d) }
                    } }
                } };
                using var result = VertexMapBuilder.Build(map, (_, _) => null, resolveAsset: _ => material);
                var marker = result.Root.GetComponentInChildren<DecalMarker>(true);
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.Guid, Is.EqualTo(id));
                Assert.That(marker.GetComponents<Collider>(), Is.Empty);
                var projector = marker.GetComponent("DecalProjector");
                Assert.That(projector, Is.Not.Null);
                object Property(string name) => projector.GetType().GetProperty(name).GetValue(projector);
                Assert.That(Property("material"), Is.SameAs(material));
                Assert.That((Vector3)Property("size"), Is.EqualTo(new Vector3(2, 1, .5f)));
                Assert.That((float)Property("fadeFactor"), Is.EqualTo(.4f).Within(.0001));
                Assert.That((float)Property("drawDistance"), Is.EqualTo(75));
                var center = marker.transform.position + marker.transform.rotation * (Vector3)Property("pivot");
                Assert.That(Vector3.Distance(center, new Vector3(2, 3, -5)), Is.LessThan(.0001));
                Assert.That(Property("scaleMode").ToString(), Is.EqualTo("ScaleInvariant"));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void NativeImportRejectsAnOrdinarySurfaceShader()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                Assert.That(VertexAssetCatalogExporter.IsDecalMaterial(material), Is.False);
                var catalog = VertexEntityCatalogExporter.Create();
                var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 1, Containers = new[] {
                    new MapContainer { Id = Guid.NewGuid(), Name = "Invalid decal", Entity = new MapEntity {
                        SdkId = catalog.SdkId, TypeId = "env_decal", Size = new Vector3Data(1, 1, 1),
                        Properties = new[] { Value("material", "asset:fixture/surface") }
                    } }
                } };
                Assert.Throws<InvalidDataException>(() => { using var result = VertexMapBuilder.Build(map, (_, _) => null, resolveAsset: _ => material); });
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        private static EntityPropertyValue Value(string id, object value) => new() { Id = id, Value = value };
    }
}
