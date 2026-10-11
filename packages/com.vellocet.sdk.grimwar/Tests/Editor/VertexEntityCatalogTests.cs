using System;
using System.Linq;
using NUnit.Framework;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vertex.Contracts;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class VertexEntityCatalogTests
    {
        [Test]
        public void CompilerCatalogComparisonIgnoresDisplayEditsButRejectsContractChanges()
        {
            var installed = VertexEntityCatalogExporter.Create();
            var original = installed.Serialize();
            var authored = EntityCatalog.Deserialize(original);
            foreach (var entity in authored.Entities)
            {
                entity.Name = "Updated label";
                entity.Category = "Updated category";
                entity.IconPng = null;
                foreach (var property in entity.Properties)
                {
                    property.Name = "Updated label";
                    property.Description = "Updated tooltip";
                    property.Group = "Updated group";
                    foreach (var choice in property.Choices) choice.Name = "Updated choice";
                }
                foreach (var warning in entity.Warnings ?? Array.Empty<EntityWarningDefinition>())
                    warning.Message = "Updated warning";
            }
            var displayEdits = authored.Serialize();
            var contract = VertexMapCompiler.EntityContract(installed);
            Assert.That(VertexMapCompiler.EntityContract(authored), Is.EqualTo(contract));
            Assert.That(installed.Serialize(), Is.EqualTo(original), "Comparison must preserve exported metadata.");
            Assert.That(authored.Serialize(), Is.EqualTo(displayEdits));

            var speed = authored.Entities.Single(entity => entity.Id == "func_door").Properties.Single(field => field.Id == "speed");
            speed.DefaultValue = 7;
            Assert.That(VertexMapCompiler.EntityContract(authored), Is.Not.EqualTo(contract), "Default changes affect omitted map values.");
            authored = EntityCatalog.Deserialize(original);
            authored.Entities.Single(entity => entity.Id == "func_door").Geometry = "bounds";
            Assert.That(VertexMapCompiler.EntityContract(authored), Is.Not.EqualTo(contract));
            authored = EntityCatalog.Deserialize(original);
            authored.SdkVersion = "different";
            Assert.That(VertexMapCompiler.EntityContract(authored), Is.Not.EqualTo(contract));
        }

        [Test]
        public void ExistingSdkMetadata_ExportsTypedDefinitionsWithoutUnitySerializationPaths()
        {
            var bytes = VertexEntityCatalogExporter.Create().Serialize();
            var catalog = EntityCatalog.Deserialize(bytes);
            Assert.That(catalog.SdkId, Is.EqualTo("com.vellocet.sdk"));
            var repeated = VertexEntityCatalogExporter.Create().Serialize();
            var mismatch = Enumerable.Range(0, Math.Min(bytes.Length, repeated.Length)).FirstOrDefault(i => bytes[i] != repeated[i]);
            Assert.That(bytes.SequenceEqual(repeated), Is.True,
                "Catalog output must not contain random marker IDs or timestamps: " +
                System.Text.Encoding.UTF8.GetString(bytes.Skip(Math.Max(0, mismatch - 100)).Take(200).ToArray()) + " versus " +
                System.Text.Encoding.UTF8.GetString(repeated.Skip(Math.Max(0, mismatch - 100)).Take(200).ToArray()));
            var door = catalog.Entities.Single(entity => entity.Id == EntityMarker.GetEntityClassName(typeof(DoorMarker)));
            var properties = door.Properties.ToDictionary(field => field.Id);
            Assert.That(door.Placement, Is.EqualTo("volume"));
            Assert.That(door.Importable, Is.True);
            Assert.That(door.Geometry, Is.EqualTo("brushes"));
            Assert.That(catalog.Entities.All(entity => entity.Importable == true), Is.True);
            Assert.That(catalog.Diagnostics, Is.Empty, "Every author-facing SDK property has a binding; compiler-owned layers/subtrees are omitted.");
            Assert.That(catalog.Entities.Single(entity => entity.Id == EntityMarker.GetEntityClassName(typeof(SpawnMarker))).Geometry, Is.EqualTo("none"));
            Assert.That(catalog.Entities.Single(entity => entity.Id == EntityMarker.GetEntityClassName(typeof(TriggerMarker))).Geometry, Is.EqualTo("bounds"));
            Assert.That(properties["speed"].Type, Is.EqualTo("number"));
            Assert.That(Convert.ToDouble(properties["speed"].DefaultValue), Is.EqualTo(6));
            Assert.That(properties["speed"].Minimum, Is.EqualTo(0.01).Within(0.000001));
            Assert.That(properties["move_direction"].Choices, Has.Length.EqualTo(6));
            Assert.That(properties["linked_doors"].Type, Is.EqualTo("entity"));
            Assert.That(properties["linked_doors"].IsArray, Is.True);
            Assert.That(properties["linked_doors"].ReferenceType, Is.EqualTo(door.Id));
            Assert.That(catalog.Entities.SelectMany(entity => entity.Properties).All(field => !string.IsNullOrWhiteSpace(field.Group)), Is.True);
            foreach (var type in new[] { "func_tracktrain", "game_blitz_tank" })
            {
                var path = catalog.Entities.Single(entity => entity.Id == type).Properties.Single(field => field.Id == "spline_container");
                Assert.That(path.Editor, Is.EqualTo("path"));
                Assert.That(path.Space, Is.EqualTo("local"));
                Assert.That(path.Units, Is.EqualTo("metres"));
                Assert.That(path.MinimumItems, Is.EqualTo(2));
                Assert.That(path.PathSmoothProperty, Is.EqualTo("path_smooth"));
            }
            Assert.That(properties.ContainsKey("guid"), Is.False);
            Assert.That(door.CreateSignalContract().TryFindInput("open", out var input) && input.Authority == EntitySignalAuthority.Server, Is.True);
            Assert.That(door.CreateSignalContract().TryFindState("state", out _), Is.True);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.That(json, Does.Not.Contain("k__BackingField"));
            Assert.That(catalog.Entities.Any(entity => entity.Id == EntityMarker.GetEntityClassName(typeof(TriggerMarker))), Is.True);
            Assert.That(catalog.Entities.Any(entity => entity.Id == EntityMarker.GetEntityClassName(typeof(SpawnMarker))), Is.True);
            Assert.That(catalog.CreateGlobalCatalog().TryGetSource("gravewar", out var events), Is.True);
            Assert.That(events["wave_started"].Parameters.Select(parameter => parameter.TypeName), Is.EqualTo(new[] { "int", "int" }));
        }
    }
}
