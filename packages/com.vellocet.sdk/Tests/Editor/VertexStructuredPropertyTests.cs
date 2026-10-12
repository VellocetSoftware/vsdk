using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Editor.Maps;
using Vertex.Contracts;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class VertexStructuredPropertyTests
    {
        [Serializable]
        public sealed class Settings
        {
            [UnityEngine.Range(1, 10)] public int Count = 3;
            [SerializeField] private string label = "default";
            public Point[] Points = Array.Empty<Point>();
            [NonSerialized] public int Ignored;
            public string Label => label;
        }

        [Serializable]
        public struct Point
        {
            public Vector3 Offset;
            public bool Enabled;
        }

        public sealed class Fixture
        {
            public Settings[] Values = { new() { Count = 7, Points = new[] { new Point { Offset = new Vector3(1, 2, 3), Enabled = true } } } };
        }

        [Test]
        public void RecordsRoundTripThroughCatalogAndImportWithSparseDefaults()
        {
            var field = EntityCatalog.Deserialize(Catalog(Describe()).Serialize()).Entities.Single().Properties.Single();
            Assert.That(field.Type, Is.EqualTo("record"));
            Assert.That(field.Members.Select(member => member.Id), Is.EquivalentTo(new[] { "count", "label", "points" }));
            field.ValidateValue(field.DefaultValue);
            var imported = Import(field.DefaultValue).Single();
            Assert.That(imported.Count, Is.EqualTo(7));
            Assert.That(imported.Label, Is.EqualTo("default"));
            Assert.That(imported.Points.Single().Offset, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(imported.Points.Single().Enabled, Is.True);

            var sparseRecord = new object[] { new object[] { "count", 9 } };
            var sparse = new object[] { sparseRecord };
            field.ValidateValue(sparse);
            imported = Import(sparse).Single();
            Assert.That(imported.Count, Is.EqualTo(9));
            Assert.That(imported.Label, Is.EqualTo(field.MemberValue(sparseRecord, "label")));
            Assert.That(imported.Points, Is.Empty);
        }

        [Test]
        public void RecordsRejectMalformedUnknownDuplicateAndOutOfRangeMembers()
        {
            var field = Describe();
            foreach (var record in new object[]
            {
                null, "invalid", new object[] { new object[] { "count" } },
                new object[] { new object[] { "unknown", 1 } },
                new object[] { new object[] { "count", 1 }, new object[] { "count", 2 } },
                new object[] { new object[] { "count", 11 } },
                new object[] { new object[] { "count", "invalid" } },
                new object[] { new object[] { "points", new object[] { new object[] { new object[] { "offset", new object[] { double.NaN, 0d, 0d } } } } } }
            })
                Assert.Throws<InvalidDataException>(() => field.ValidateValue(new object[] { record }));
        }

        [Test]
        public void StructuredSchemasEnforceDepthMemberAndReferenceLimits()
        {
            var field = new EntityPropertyDefinition { Id = "value", Name = "Value", Type = "bool", DefaultValue = false };
            for (var i = 0; i < 3; i++) field = Record(field);
            Assert.DoesNotThrow(field.Validate);
            Assert.Throws<InvalidDataException>(Record(field).Validate);
            var wide = Record(field);
            wide.Members = Enumerable.Range(0, 65).Select(i => new EntityPropertyDefinition
                { Id = "field_" + i, Name = "Field", Type = "bool", DefaultValue = false }).ToArray();
            Assert.Throws<InvalidDataException>(wide.Validate);
            Assert.Throws<InvalidDataException>(Record(new EntityPropertyDefinition
                { Id = "target", Name = "Target", Type = "entity", DefaultValue = null }).Validate);
        }

        [Test]
        public void MapValuesBoundDepthAndShareOneBudgetAcrossProperties()
        {
            object value = false;
            for (var i = 0; i < 16; i++) value = new object[] { value };
            var entity = Entity(value);
            Assert.DoesNotThrow(entity.Validate);
            entity.Properties[0].Value = new object[] { value };
            Assert.Throws<InvalidDataException>(entity.Validate);
            var row = Enumerable.Repeat<object>(false, 256).ToArray();
            entity = Entity(Enumerable.Repeat<object>(row, 255).ToArray());
            Assert.DoesNotThrow(entity.Validate);
            entity.Properties = entity.Properties.Concat(new[] { new EntityPropertyValue { Id = "extra", Value = false } }).ToArray();
            Assert.Throws<InvalidDataException>(entity.Validate);
            entity = Entity(Enumerable.Repeat<object>(row, 256).ToArray());
            Assert.Throws<InvalidDataException>(entity.Validate);
        }

        [Test]
        public void NestedDisplayEditsPreserveTheCompilerContractButValueChangesDoNot()
        {
            var catalog = Catalog(Describe());
            var original = VertexMapCompiler.EntityContract(catalog);
            var count = catalog.Entities.Single().Properties.Single().Members.Single(member => member.Id == "count");
            count.Name = "Updated label";
            count.Description = "Updated tooltip";
            count.Group = "Updated group";
            Assert.That(VertexMapCompiler.EntityContract(catalog), Is.EqualTo(original));
            count.DefaultValue = 4;
            Assert.That(VertexMapCompiler.EntityContract(catalog), Is.Not.EqualTo(original));
        }

        private static EntityPropertyDefinition Describe()
        {
            var field = typeof(Fixture).GetField(nameof(Fixture.Values));
            return (EntityPropertyDefinition)typeof(VertexEntityCatalogExporter).GetMethod("Describe", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { field, "values", "Values", field.GetValue(new Fixture()), 0 });
        }

        private static Settings[] Import(object value) => (Settings[])typeof(VertexEntityImporter)
            .GetMethod("ConvertValue", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { typeof(Settings[]), value, null, null });

        private static EntityPropertyDefinition Record(EntityPropertyDefinition member) => new()
            { Id = "record", Name = "Record", Type = "record", DefaultValue = Array.Empty<object>(), Members = new[] { member } };

        private static EntityCatalog Catalog(EntityPropertyDefinition field) => new()
        {
            SdkId = "test.sdk", SdkVersion = "1", Entities = new[] { new EntityTypeDefinition
                { Id = "test_entity", Name = "Test entity", Category = "Tests", Placement = "point", Properties = new[] { field } } }
        };

        private static MapEntity Entity(object value) => new()
            { SdkId = "test.sdk", TypeId = "test_entity", Properties = new[] { new EntityPropertyValue { Id = "values", Value = value } } };
    }
}
