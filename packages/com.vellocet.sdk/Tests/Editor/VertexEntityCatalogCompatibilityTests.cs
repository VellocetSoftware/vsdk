using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Editor.Maps;
using Vertex.Contracts;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class VertexEntityCatalogCompatibilityTests
    {
        [Test]
        public void SaveValidationAllowsUnusedCatalogChangesAndReportsUsedPropertyMismatch()
        {
            var root = Path.Combine(Path.GetTempPath(), "vertex-catalog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "VertexJob/Catalogs"));
            try
            {
                var map = Map("func_door");
                var job = new BuildJob { MapId = map.MapId, Map = "Map.vertexmesh",
                    Game = new GameProfile { Baking = VertexBakePolicy.Create() } };
                File.WriteAllBytes(PathFor(job.Map), map.Serialize());
                var authored = VertexEntityCatalogExporter.Create();
                authored.Entities = authored.Entities.Concat(new[] { Definition("removed_unused_entity") }).ToArray();
                var path = PathFor("VertexJob/Catalogs/" + authored.SdkId + ".vertexentities");
                File.WriteAllBytes(path, authored.Serialize());
                Assert.DoesNotThrow(() => VertexMapCompiler.ValidateAuthoring(job, PathFor));
                authored.Entities.Single(entity => entity.Id == "func_door").Properties.Single(field => field.Id == "speed").DefaultValue = 7;
                File.WriteAllBytes(path, authored.Serialize());
                var error = Assert.Throws<InvalidDataException>(() => VertexMapCompiler.ValidateAuthoring(job, PathFor));
                Assert.That(error.Message, Does.Contain("func_door").And.Contain("speed").And.Contain("default"));
                Assert.That(error.Message, Does.Contain("installed.vertexentities"));
                Assert.DoesNotThrow(() => EntityCatalog.Deserialize(File.ReadAllBytes(PathFor("VertexJob/installed.vertexentities"))));
            }
            finally { Directory.Delete(root, true); }
            string PathFor(string path) => Path.Combine(root, path);
        }

        [Test]
        public void CompatibleAdditionsOrderingAndDisplayChangesPreserveBothCatalogs()
        {
            var authored = Catalog();
            var installed = Catalog();
            installed.Entities[0].Name = "New label";
            installed.Entities[0].Properties[0].Group = "New group";
            installed.Entities[0].Properties = installed.Entities[0].Properties.Concat(new[] {
                new EntityPropertyDefinition { Id = "new_option", Name = "New option", Type = "bool", DefaultValue = false }
            }).Reverse().ToArray();
            installed.Entities[0].Signals = installed.Entities[0].Signals.Concat(new[] {
                new EntitySignalDefinition { Name = "new_input", Kind = EntitySignalMemberKind.Input, Authority = EntitySignalAuthority.Server }
            }).Reverse().ToArray();
            installed.Entities = installed.Entities.Concat(new[] { Definition("new_type") }).Reverse().ToArray();
            installed.Globals = installed.Globals.Concat(new[] { new GlobalEventDefinition { Source = "game", Name = "new_event" } }).Reverse().ToArray();
            installed.Diagnostics = new[] { new EntityCatalogDiagnostic { Code = "unused_field", Message = "Editor diagnostic", EntityId = "new_type" } };
            authored.Entities = authored.Entities.Concat(new[] { Definition("removed_type") }).ToArray();
            var before = authored.Serialize();
            var after = installed.Serialize();
            Assert.That(VertexMapCompiler.EntityContractDifference(authored, installed, Map("test_entity")), Is.Null);
            Assert.That(authored.Serialize(), Is.EqualTo(before));
            Assert.That(installed.Serialize(), Is.EqualTo(after));
        }

        [TestCase("default")]
        [TestCase("type")]
        [TestCase("geometry")]
        [TestCase("authority")]
        [TestCase("removed_property")]
        [TestCase("removed_entity")]
        public void UsedContractsRemainStrictButUnusedTypesDoNotBlockSaving(string change)
        {
            var authored = Catalog();
            var installed = Catalog();
            var entity = installed.Entities[0];
            switch (change)
            {
                case "default": entity.Properties[0].DefaultValue = 2; break;
                case "type": entity.Properties[0].Type = "integer"; break;
                case "geometry": entity.Geometry = "bounds"; break;
                case "authority": entity.Signals[0].Authority = EntitySignalAuthority.Client; break;
                case "removed_property": entity.Properties = Array.Empty<EntityPropertyDefinition>(); break;
                case "removed_entity": installed.Entities = Array.Empty<EntityTypeDefinition>(); break;
            }
            var mismatch = VertexMapCompiler.EntityContractDifference(authored, installed, Map("test_entity"));
            Assert.That(mismatch, Does.Contain("test_entity"));
            if (change is "default" or "type") Assert.That(mismatch, Does.Contain("speed").And.Contain(change));
            Assert.That(VertexMapCompiler.EntityContractDifference(authored, installed, Map()), Is.Null);
        }

        [Test]
        public void CatalogIdentityGlobalsAndMalformedMetadataRemainValidated()
        {
            var authored = Catalog();
            var installed = Catalog();
            installed.SdkVersion = "2";
            Assert.That(VertexMapCompiler.EntityContractDifference(authored, installed, Map()), Does.Contain("sdkVersion"));
            installed = Catalog();
            installed.Globals[0].Parameters = new[] { new SignalParameterDefinition { Name = "value", TypeName = "int" } };
            Assert.That(VertexMapCompiler.EntityContractDifference(authored, installed, Map()), Does.Contain("game.ready"));
            installed.Globals = Array.Empty<GlobalEventDefinition>();
            Assert.That(VertexMapCompiler.EntityContractDifference(authored, installed, Map()), Does.Contain("missing"));
            installed = Catalog();
            installed.Entities[0].Properties[0].DefaultValue = "invalid";
            Assert.Throws<InvalidDataException>(() => VertexMapCompiler.EntityContractDifference(authored, installed, Map()));
        }

        [Test]
        public void AvatarModelsDoNotChangeTheCompilationContract()
        {
            var authored = Catalog();
            var installed = Catalog();
            foreach (var catalog in new[] { authored, installed })
            {
                catalog.Entities[0].Placement = "point";
                catalog.Entities[0].Geometry = "none";
            }
            authored.Entities[0].AvatarModel = "asset:game.models/old";
            installed.Entities[0].AvatarModel = "asset:game.models/new";
            CollectionAssert.AreEqual(VertexMapCompiler.EntityContract(authored), VertexMapCompiler.EntityContract(installed));
            Assert.That(VertexMapCompiler.EntityContractDifference(authored, installed, Map("test_entity")), Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AvatarVisibilityAndVariantsDoNotChangeCompilationButTheirPropertyContractsRemainStrict(bool authoredHasAvatar)
        {
            var authored = Catalog();
            var installed = Catalog();
            foreach (var catalog in new[] { authored, installed })
            {
                catalog.Entities[0].Placement = "point";
                catalog.Entities[0].Geometry = "none";
            }
            var visible = (authoredHasAvatar ? authored : installed).Entities[0];
            visible.AvatarEnabledProperty = "enabled";
            visible.AvatarVariants = new[] { new EntityAvatarVariant
                { Property = "speed", Value = 1, Model = "asset:game.models/avatar" } };
            var before = authored.Serialize();
            var after = installed.Serialize();
            CollectionAssert.AreEqual(VertexMapCompiler.EntityContract(authored), VertexMapCompiler.EntityContract(installed));
            Assert.That(VertexMapCompiler.EntityContractDifference(authored, installed, Map("test_entity")), Is.Null);
            CollectionAssert.AreEqual(before, authored.Serialize());
            CollectionAssert.AreEqual(after, installed.Serialize());
            installed.Entities[0].Properties.Single(property => property.Id == "enabled").DefaultValue = false;
            Assert.That(VertexMapCompiler.EntityContractDifference(authored, installed, Map("test_entity")),
                Does.Contain("enabled").And.Contain("default"));
        }

        private static EntityCatalog Catalog() => new() { SdkId = "com.vellocet.sdk", SdkVersion = "1",
            Entities = new[] { Definition("test_entity") },
            Globals = new[] { new GlobalEventDefinition { Source = "game", Name = "ready" } } };

        private static EntityTypeDefinition Definition(string id) => new() { Id = id, Name = id, Category = "Test",
            Placement = "volume", Geometry = "brushes", Importable = true,
            Properties = new[] { new EntityPropertyDefinition { Id = "speed", Name = "Speed", Type = "number", DefaultValue = 1 },
                new EntityPropertyDefinition { Id = "enabled", Name = "Enabled", Type = "bool", DefaultValue = true } },
            Signals = new[] { new EntitySignalDefinition { Name = "open", Kind = EntitySignalMemberKind.Input, Authority = EntitySignalAuthority.Server } } };

        private static MapAsset Map(params string[] types) => new() { MapId = Guid.NewGuid(), MetersPerUnit = 1,
            Containers = types.Select(type => new MapContainer { Id = Guid.NewGuid(), Name = type,
                Entity = new MapEntity { SdkId = "com.vellocet.sdk", TypeId = type } }).ToArray() };
    }
}
