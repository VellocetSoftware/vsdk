using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Editor.VSig;
using Vellocet.Sdk.VSig;
using Vellocet.Sdk.VSig.Runtime;
using Vertex.Contracts;
using Vertex.Unity;
using Vertex.Unity.Editor;
using Vertex.VSig;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class VertexEntityImportTests
    {
        [Test]
        public void RepeatedScriptHostsImportTheirOwnEntitiesAndCompileIndependentModules()
        {
            var library = new TextAsset("module control:\n    ref door: func_door\n    input open:\n        door.open\n");
            try
            {
                var hosts = new[] { Entity("Spawn Doors", "logic_script", new(0, 0, 0)), Entity("Spawn Doors", "logic_script", new(10, 0, 0)) };
                foreach (var host in hosts) host.Entity.Properties = new[]
                {
                    new EntityPropertyValue { Id = "library", Value = "asset:test/library" },
                    new EntityPropertyValue { Id = "module_name", Value = "control" },
                    new EntityPropertyValue { Id = "alias", Value = "logic_script_{id}" },
                    new EntityPropertyValue { Id = "bind_to_hierarchy", Value = true }
                };
                var doors = hosts.Select(host =>
                {
                    var door = Entity("Door", "func_door", host.Entity.Position);
                    door.ParentId = host.Id;
                    return door;
                }).ToArray();
                var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 1,
                    Containers = hosts.Concat(doors).ToArray(), Objects = doors.Select(Geometry).ToArray() };
                using (var built = VertexMapBuilder.Build(map, null, resolveAsset: _ => library))
                {
                    var markers = built.Root.GetComponentsInChildren<EntityMarker>(true);
                    var diagnostics = new List<VSigDiagnostic>();
                    var program = VSigSceneCompiler.Parse("", diagnostics, markers, "Map.vsig");
                    VSigSceneCompiler.Validate(program, markers, diagnostics);
                    var aliases = VSigSceneCompiler.ResolveAliases(program, markers, diagnostics);
                    Assert.That(diagnostics, Is.Empty);
                    for (var i = 0; i < hosts.Length; i++)
                        Assert.That(aliases["logic_script_" + hosts[i].Id.ToString("N") + "_door"].Single().Guid,
                            Is.EqualTo(doors[i].Id), "A reusable host only binds its own hierarchy.");
                }
                doors[1].ParentId = doors[0].Id;
                Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null, resolveAsset: _ => library),
                    "Allowing logic hosts must not allow unrelated nested entity ownership.");
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void ParticlePlacementsImportNativePresentationAndRejectBlockers()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var folder = "Assets/ParticleBindingCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var source = new GameObject("Effect-only prefab", typeof(ParticleSystem));
            source.SetActive(false);
            try
            {
                var field = VertexEntityCatalogExporter.Create().Entities.Single(item => item.Id == "env_particle").Properties.Single(item => item.Id == "effect");
                Assert.That(field.AssetRole, Is.EqualTo("effect"));
                GameObjectUtility.SetStaticEditorFlags(source, StaticEditorFlags.OccluderStatic | StaticEditorFlags.ContributeGI);
                var entity = Entity("Effect", "env_particle", new(2, 4, 6));
                source.transform.localScale = new Vector3(.5f, 2, 3);
                entity.Entity.Properties = new[] { new EntityPropertyValue { Id = "effect", Value = "asset:test/effect" },
                    new EntityPropertyValue { Id = "effect_scale", Value = new object[] { 2d, 3d, .5d } } };
                var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = .25, Containers = new[] { entity } };
                using (var built = VertexMapBuilder.Build(map, null, resolveAsset: _ => source))
                {
                    var marker = built.Root.GetComponentInChildren<ParticleMarker>(true);
                    Assert.That(marker.Guid, Is.EqualTo(entity.Id));
                    Assert.That(marker.RequiresRuntimeEntity, Is.False);
                    Assert.That(marker.Presentation && marker.Presentation.activeSelf, Is.True);
                    Assert.That(marker.Presentation.transform.position, Is.EqualTo(VertexMapBuilder.Position(entity.Entity.Position, .25)));
                    Assert.That(marker.Presentation.transform.localScale, Is.EqualTo(new Vector3(1, 6, 1.5f)),
                        "Placement scale multiplies the native prefab size without a map-unit or coordinate conversion.");
                    Assert.That(marker.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0), Is.True);
                    Assert.That(marker.IgnoreNavMesh, Is.True);
                    var scriptPath = folder + "/Map.vsig";
                    File.WriteAllText(scriptPath, "ref effect: env_particle\non map.ready:\n    effect.play()\n");
                    AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceSynchronousImport);
                    Assert.That(VSigCompiledSceneInjector.InjectSource(scene, AssetDatabase.LoadAssetAtPath<TextAsset>(scriptPath), null, out var message), Is.True, message);
                    Assert.That(marker.RequiresRuntimeEntity && !marker.Presentation.activeSelf, Is.True);
                    File.WriteAllText(scriptPath, "# No referenced effects\n");
                    AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    Assert.That(VSigCompiledSceneInjector.InjectSource(scene, AssetDatabase.LoadAssetAtPath<TextAsset>(scriptPath), null, out message), Is.True, message);
                    Assert.That(!marker.RequiresRuntimeEntity && marker.Presentation.activeSelf, Is.True);
                }
                entity.Entity.Properties = entity.Entity.Properties.Concat(new[] { new EntityPropertyValue { Id = "ignore_nav_mesh", Value = false } }).ToArray();
                Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null, resolveAsset: _ => source));
                entity.Entity.Properties = entity.Entity.Properties.Take(1).ToArray();
                var attachedScenery = new GameObject("Scenery with an emitter", typeof(MeshRenderer));
                attachedScenery.transform.SetParent(source.transform);
                Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null, resolveAsset: _ => source));
                var meshEffect = PrefabUtility.SaveAsPrefabAsset(source, folder + "/MeshEffect.prefab");
                Assert.That(VertexAssetCatalogExporter.IsEffectPrefab(meshEffect), Is.False);
                AssetDatabase.SetLabels(meshEffect, new[] { "VertexEffect" });
                Assert.That(VertexAssetCatalogExporter.IsEffectPrefab(meshEffect), Is.True, "Mesh effects require explicit authoring intent.");
                Object.DestroyImmediate(attachedScenery);
                source.AddComponent<BoxCollider>();
                Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null, resolveAsset: _ => source));
                var scenery = new GameObject("Unrelated scenery prefab");
                try { Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null, resolveAsset: _ => scenery)); }
                finally { Object.DestroyImmediate(scenery); }
            }
            finally
            {
                Object.DestroyImmediate(source);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void AuthoredPathMatchesVertexPreview(bool smooth, bool closed)
        {
            var points = new[] { new Vector3Data(0, 0, 0), new Vector3Data(2, 1, -4), new Vector3Data(5, 2, -3) };
            foreach (var type in new[] { "func_tracktrain", "game_blitz_tank" })
            {
                var entity = Entity("Path", type, new(20, 4, 6));
                entity.Entity.Properties = new[]
                {
                    new EntityPropertyValue { Id = "spline_container", Value = points.Select(p => (object)new object[] { p.X, p.Y, p.Z }).ToArray() },
                    new EntityPropertyValue { Id = "path_smooth", Value = smooth },
                    new EntityPropertyValue { Id = "path_closed", Value = closed }
                };
                var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = .25, Containers = new[] { entity },
                    Objects = type == "func_tracktrain" ? new[] { Geometry(entity) } : Array.Empty<MapObject>() };
                using var built = VertexMapBuilder.Build(MapAsset.Deserialize(map.Serialize()), null);
                var path = built.Root.GetComponentInChildren<SplineContainer>();
                Assert.That(path.transform.parent, Is.EqualTo(built.Root.transform));
                Assert.That(path.transform.position, Is.EqualTo(VertexMapBuilder.Position(entity.Entity.Position, map.MetersPerUnit)),
                    "Paths use the authored pivot, before the SDK adapts a volume marker's origin to its base.");
                Assert.That(path.Spline.Closed, Is.EqualTo(closed));
                var preview = EntityPath.Sample(points, smooth, closed);
                var steps = smooth ? 16 : 1;
                for (var i = 0; i < preview.Length - 1; i++)
                {
                    var actual = CurveUtility.EvaluatePosition(path.Spline.GetCurve(i / steps), (float)(i % steps) / steps);
                    var expected = preview[i];
                    Assert.That(Vector3.Distance((Vector3)actual, new Vector3((float)expected.X, (float)expected.Y, -(float)expected.Z)), Is.LessThan(.00001));
                }
            }
        }

        [Test]
        public void PublishedBakePolicyMatchesInstalledDefaultsAndRejectsOverrides()
        {
            var policy = VertexBakePolicy.Create();
            policy.Validate(); VertexBakePolicy.Validate(policy);
            Assert.That(policy.Settings.Single(s => s.Name == "spatial.MaxDepth").Value, Is.EqualTo("8"));
            Assert.That(policy.Settings.Single(s => s.Name == "occlusion.smallestHole").Value, Is.EqualTo("0.25"));
            foreach (var choice in new[] { "None", "Low", "Medium", "High" })
            {
                var map = MapAsset.Deserialize(new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 1, LightmapBake = choice }.Serialize());
                Assert.That(VertexBakePolicy.Lightmaps(map, policy), Is.EqualTo(choice));
                Assert.DoesNotThrow(() => VertexBakePolicy.Quality(choice));
            }
            Assert.That(VertexBakePolicy.Lightmaps(new MapAsset(), policy), Is.EqualTo(policy.DefaultLightmaps));
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scales = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)).ToDictionary(r => r, r => r.scaleInLightmap);
            var occlusion = (StaticOcclusionCulling.smallestHole, StaticOcclusionCulling.smallestOccluder, StaticOcclusionCulling.backfaceThreshold);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                cube.GetComponent<MeshRenderer>().scaleInLightmap = 3;
                StaticOcclusionCulling.smallestHole = 7;
                VertexBakePolicy.Apply(scene, policy);
                Assert.That(cube.GetComponent<MeshRenderer>().scaleInLightmap, Is.EqualTo(1));
                Assert.That(StaticOcclusionCulling.smallestHole, Is.EqualTo(.25f));
                Assert.That(StaticOcclusionCulling.smallestOccluder, Is.EqualTo(5));
                Assert.That(StaticOcclusionCulling.backfaceThreshold, Is.EqualTo(100));
            }
            finally
            {
                Object.DestroyImmediate(cube);
                foreach (var pair in scales) if (pair.Key) pair.Key.scaleInLightmap = pair.Value;
                StaticOcclusionCulling.smallestHole = occlusion.Item1;
                StaticOcclusionCulling.smallestOccluder = occlusion.Item2;
                StaticOcclusionCulling.backfaceThreshold = occlusion.Item3;
            }
            policy.Settings[0].Value = "changed";
            Assert.Throws<InvalidDataException>(() => VertexBakePolicy.Validate(policy));
            var catalog = VertexEntityCatalogExporter.Create();
            var light = EntityCatalog.Deserialize(catalog.Serialize()).Entities.Single(e => e.Id == "env_light");
            Assert.That(light.IconBytes(), Is.Not.Empty);
            light.IconPng = Convert.ToBase64String(new byte[40]);
            Assert.Throws<InvalidDataException>(() => light.IconBytes());
        }

        [Test]
        public void LightsDefaultToBakedAndOnlyScriptedLightsNeedNetworkEntities()
        {
            var definition = VertexEntityCatalogExporter.Create().Entities.Single(entity => entity.Id == "env_light");
            var warning = definition.Warnings.Single(rule => rule.Code == "LIGHT_EFFECT_BAKED");
            Assert.That(warning.Matches(id => id == "effect" ? 1 : 0), Is.True);
            Assert.That(warning.Matches(id => id == "effect" ? 1 : 1), Is.False);
            Assert.That(warning.Matches(id => id == "effect" ? 0 : 0), Is.False);
            Assert.That(definition.Properties.Single(field => field.Id == "light_type").Choices.Select(choice => choice.Name),
                Is.EquivalentTo(new[] { "Point", "Spot", "Area" }));
            var source = Entity("Test light", "env_light", new(0, 0, 0));
            var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 1, Containers = new[] { source } };
            foreach (var type in new[] { LightType.Point, LightType.Spot })
            foreach (var mode in new[] { LightMarker.LightingMode.Baked, LightMarker.LightingMode.Mixed, LightMarker.LightingMode.Dynamic })
            foreach (var effect in new[] { LightMarker.LightEffect.None, LightMarker.LightEffect.Flicker, LightMarker.LightEffect.Strobe, LightMarker.LightEffect.Candle })
            {
                source.Entity.Properties = new[] {
                    new EntityPropertyValue { Id = "light_type", Value = (int)type },
                    new EntityPropertyValue { Id = "lighting", Value = (int)mode },
                    new EntityPropertyValue { Id = "effect", Value = (int)effect }
                };
                using var built = VertexMapBuilder.Build(map, null);
                var marker = built.Root.GetComponentInChildren<LightMarker>(true);
                var light = marker.GetComponent<Light>();
                Assert.That(marker.RequiresRuntimeEntity, Is.False);
                Assert.That(light.type, Is.EqualTo(type));
                Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
                var expected = mode == LightMarker.LightingMode.Mixed ? LightmapBakeType.Mixed :
                    mode == LightMarker.LightingMode.Dynamic || effect != LightMarker.LightEffect.None ? LightmapBakeType.Realtime : LightmapBakeType.Baked;
                Assert.That(light.lightmapBakeType, Is.EqualTo(expected));
                Assert.That(marker.GetComponent<LightEffectPlayer>(), Is.Null, "Effects are only instantiated at runtime.");
                marker.SetScriptReferenced(true);
                Assert.That(marker.RequiresRuntimeEntity, Is.True);
                Assert.That(light.lightmapBakeType, Is.EqualTo(mode == LightMarker.LightingMode.Mixed ? LightmapBakeType.Mixed : LightmapBakeType.Realtime));
                marker.SetScriptReferenced(false);
                Assert.That(marker.RequiresRuntimeEntity, Is.False);
                Assert.That(light.lightmapBakeType, Is.EqualTo(expected));
            }
            source.Entity.Properties = new[] { new EntityPropertyValue { Id = "light_type", Value = (int)LightType.Rectangle } };
            using (var built = VertexMapBuilder.Build(map, null))
                Assert.That(built.Root.GetComponentInChildren<Light>(true).lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked));
            source.Entity.Properties = source.Entity.Properties.Concat(new[] { new EntityPropertyValue { Id = "scripted", Value = true } }).ToArray();
            Assert.That(Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null)).Message, Does.Contain("baked-only"));
            source.Entity.Properties = new[] { new EntityPropertyValue { Id = "light_type", Value = (int)LightType.Directional } };
            Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null));
            Assert.That(LightEffectPlayer.Brightness(LightMarker.LightEffect.None, long.MaxValue), Is.EqualTo(1));
            Assert.That(LightEffectPlayer.Brightness(LightMarker.LightEffect.Flicker, 0), Is.EqualTo(1));
            Assert.That(LightEffectPlayer.Brightness(LightMarker.LightEffect.Flicker, 23), Is.EqualTo(1));
            Assert.That(LightEffectPlayer.Brightness(LightMarker.LightEffect.Strobe, 0), Is.Zero);
            Assert.That(LightEffectPlayer.Brightness(LightMarker.LightEffect.Strobe, 8), Is.EqualTo(25f / 12));
            Assert.That(LightEffectPlayer.Brightness(LightMarker.LightEffect.Candle, 26), Is.EqualTo(1));
        }

        [Test]
        public void ModelsShareAssetsAndObeyTheirOwnersMobilityWithoutChangingSourcePrefabs()
        {
            var folder = "Assets/VertexModelPolicyCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var path = folder + "/Prop.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(cube, path);
                var map = Fixture();
                var model = new MapContainer { Id = Guid.NewGuid(), Name = "Static prop", Model = new MapModel { Asset = "asset:test/prop" } };
                var moving = new MapContainer { Id = Guid.NewGuid(), Name = "Moving prop", ParentId = map.Containers[2].Id,
                    Model = new MapModel { Asset = "asset:test/prop" } };
                map.Containers = map.Containers.Concat(new[] { model, moving }).ToArray();
                using (var built = VertexMapBuilder.Build(map, null, _ => prefab))
                {
                    var ids = built.Root.GetComponentsInChildren<VertexObjectIdentity>().ToDictionary(value => value.ObjectId);
                    var stationary = ids[model.Id].GetComponentInChildren<MeshRenderer>();
                    var mobile = ids[moving.Id].GetComponentInChildren<MeshRenderer>();
                    Assert.That(stationary.staticShadowCaster, Is.True);
                    Assert.That(mobile.staticShadowCaster, Is.False);
                    Assert.That(GameObjectUtility.GetStaticEditorFlags(stationary.gameObject).HasFlag(StaticEditorFlags.ContributeGI), Is.True);
                    Assert.That(GameObjectUtility.GetStaticEditorFlags(mobile.gameObject), Is.EqualTo((StaticEditorFlags)0));
                    Assert.That(mobile.receiveGI, Is.EqualTo(ReceiveGI.LightProbes));
                    Assert.That(stationary.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mobile.GetComponent<MeshFilter>().sharedMesh));
                    Assert.That(prefab.isStatic, Is.False, "Only instances change; installed source prefabs stay untouched.");
                    Assert.That(prefab.GetComponent<MeshRenderer>().staticShadowCaster, Is.False);
                    Vellocet.Sdk.Editor.Maps.VertexMapPolicy.Apply(map, built.Root, compiling: true);
                    Assert.That(ids[moving.Id].GetComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild, Is.True);
                    Assert.That(ids[model.Id].GetComponent<Unity.AI.Navigation.NavMeshModifier>(), Is.Null);
                }
                cube.AddComponent<Rigidbody>(); PrefabUtility.SaveAsPrefabAsset(cube, path);
                Assert.That(Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null, _ => prefab)).Message, Does.Contain("Rigidbody"));
            }
            finally { Object.DestroyImmediate(cube); AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void StationaryBrushesCastStaticShadowsAndCompilationKeepsMovingGeometryDynamic()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var map = Fixture();
                var stationary = Geometry(map.Containers[2]);
                stationary.ParentId = null;
                map.Objects = map.Objects.Concat(new[] { stationary }).ToArray();
                using var built = VertexMapBuilder.Build(map, null);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(built.Root, scene);
                var ids = built.Root.GetComponentsInChildren<VertexObjectIdentity>().ToDictionary(value => value.ObjectId);
                var wall = ids[stationary.Id].GetComponent<MeshRenderer>();
                var door = ids[map.Objects[0].Id].GetComponent<MeshRenderer>();
                Assert.That(wall.staticShadowCaster, Is.True, "Stationary brushes must be static shadow casters on import.");
                Assert.That(door.staticShadowCaster, Is.False, "Owned moving brushes must stay dynamic on import.");

                // The compile pass must repair older scenes and respect motion inherited from parents.
                wall.staticShadowCaster = false;
                door.staticShadowCaster = true;
                door.gameObject.isStatic = true;
                var animated = new GameObject("Animated parent", typeof(Animator));
                animated.transform.SetParent(built.Root.transform, false);
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.transform.SetParent(animated.transform, false);
                child.isStatic = true;
                child.GetComponent<MeshRenderer>().staticShadowCaster = true;
                Assert.That(MapRendererOptimizationUtility.Optimize(scene, out var message), Is.True, message);
                Assert.That(wall.staticShadowCaster, Is.True);
                Assert.That(door.staticShadowCaster, Is.False);
                Assert.That(child.GetComponent<MeshRenderer>().staticShadowCaster, Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void EntireCatalogImportsWithTypedAssetsPathsLightsVolumesAndStableOwnership()
        {
            var catalog = VertexEntityCatalogExporter.Create();
            var containers = catalog.Entities.Select((type, index) => Entity(type.Name, type.Id, new(index * 20, 4, 6))).ToArray();
            var tank = containers.Single(node => node.Entity.TypeId == "game_blitz_tank");
            containers.Single(node => node.Entity.TypeId == "game_blitz_checkpoint").Entity.Properties = new[]
                { new EntityPropertyValue { Id = "tank", Value = tank.Id.ToString("D") } };
            var water = containers.Single(node => node.Entity.TypeId == "trigger_water");
            water.Entity.Properties = new[] { new EntityPropertyValue { Id = "surface_material", Value = "asset:test/water" } };
            var light = containers.Single(node => node.Entity.TypeId == "env_light");
            var particle = containers.Single(node => node.Entity.TypeId == "env_particle");
            particle.Entity.Properties = new[] { new EntityPropertyValue { Id = "effect", Value = "asset:test/effect" } };
            light.Entity.Properties = new[] { new EntityPropertyValue { Id = "intensity", Value = 3d }, new EntityPropertyValue { Id = "enabled", Value = false } };
            var map = new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = .5, Containers = containers,
                Objects = containers.Where(node => catalog.Entities.Single(type => type.Id == node.Entity.TypeId).Geometry == "brushes").Select(Geometry).ToArray() };
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            var effect = new GameObject("Effect", typeof(ParticleSystem));
            effect.SetActive(false);
            try
            {
                using (var result = VertexMapBuilder.Build(MapAsset.Deserialize(map.Serialize()), null, resolveAsset: key => key == "asset:test/water" ? material : key == "asset:test/effect" ? effect : null))
                {
                    var markers = result.Root.GetComponentsInChildren<EntityMarker>(true);
                    Assert.That(markers.Select(marker => marker.Guid), Is.EquivalentTo(containers.Select(node => node.Id)));
                    Assert.That(markers.OfType<TriggerWaterMarker>().Single().SurfaceMaterial, Is.SameAs(material));
                    Assert.That(markers.OfType<LightMarker>().Single().GetComponent<Light>().intensity, Is.EqualTo(3));
                    Assert.That(markers.OfType<LightMarker>().Single().GetComponent<Light>().enabled, Is.False);
                    Assert.That(markers.OfType<LightMarker>().Single().GetComponent<Light>().lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked));
                    Assert.That(markers.OfType<LabelMarker>().Single().Label.text, Is.EqualTo("Label"));
                    var label = markers.OfType<LabelMarker>().Single();
                    Assert.That(label.Label.alignment, Is.EqualTo(TMPro.TextAlignmentOptions.Center), "Vertex's authored position is the text center.");
                    label.PrepareVertexPackage();
                    Assert.That(label.GetComponent<TMPro.TextMeshPro>(), Is.Null, "Packaged text must not serialize auto-restored base fonts.");
                    Assert.That(label.AuthoredText, Is.EqualTo("Label"), "Dedicated servers retain label state without constructing renderers.");
                    typeof(LabelMarker).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(label, null);
                    Assert.That(label.Label.text, Is.EqualTo("Label"), "The installed renderer is reconstructed from the authored text data.");
                    Assert.That(label.Label.alignment, Is.EqualTo(TMPro.TextAlignmentOptions.Center), "Bundled labels keep the same placement as base maps.");
                    var train = markers.OfType<TrackTrainMarker>().Single();
                    Assert.That(train.HasExternalSpline, Is.True);
                    Assert.That(train.SplineContainer.Spline.Count, Is.EqualTo(2));
                    Assert.That(markers.OfType<BlitzCheckpointMarker>().Single().transform.parent, Is.EqualTo(markers.OfType<BlitzTankMarker>().Single().transform));
                    foreach (var marker in markers.Where(marker => marker is IMovingBrushEntityMarker or DamageableMarker))
                    {
                        Assert.That(marker.IgnoreNavMesh, Is.True);
                        Assert.That(marker.GetComponentsInChildren<MeshRenderer>().All(renderer => renderer.receiveGI == ReceiveGI.LightProbes && !renderer.staticShadowCaster), Is.True);
                        Assert.That(marker.GetComponentsInChildren<Transform>().All(t => GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0), Is.True);
                    }
                    Assert.That(markers.OfType<LadderMarker>().Single().GetBoundsXYZ(), Is.EqualTo(Vector3.one * 16));
                }
                Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null, resolveAsset: _ => null));
                water.Entity.Properties = Array.Empty<EntityPropertyValue>();
                var folder = "Assets/VertexAllEntityCheck_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                try
                {
                    var effectPath = folder + "/Effect.prefab";
                    var effectAsset = PrefabUtility.SaveAsPrefabAsset(effect, effectPath);
                    var profile = ScriptableObject.CreateInstance<VertexAssetCatalogProfile>();
                    profile.catalogId = "particle.fixture";
                    profile.folders = new[] { new VertexAssetFolder { folder = folder } };
                    AssetDatabase.CreateAsset(profile, folder + "/Assets.asset");
                    particle.Entity.Properties[0].Value = "asset:particle.fixture/" + VertexAssetCatalogExporter.NativeId(effectAsset);
                    var path = folder + "/All.vertexmesh";
                    File.WriteAllBytes(path, map.Serialize()); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert.That(imported, Is.Not.Null);
                    var ids = FileIds(imported);
                    Array.Reverse(map.Containers);
                    File.WriteAllBytes(path, map.Serialize()); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    Assert.That(FileIds(AssetDatabase.LoadAssetAtPath<GameObject>(path)), Is.EquivalentTo(ids), "All bound marker IDs survive a reordered reimport.");
                }
                finally { AssetDatabase.DeleteAsset(folder); }
                containers.Single(node => node.Entity.TypeId == "func_tracktrain").Entity.Properties = new[]
                    { new EntityPropertyValue { Id = "spline_container", Value = new object[] { new object[] { 0d, 0d, 0d } } } };
                Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null));
            }
            finally { Object.DestroyImmediate(material); Object.DestroyImmediate(effect); }
        }

        [Test]
        public void SaveOnly_AcceptsUnfinishedScriptAndKeepsAuthoringIdentities()
        {
            var folder = "Assets/VertexSourceSaveCheck_" + Guid.NewGuid().ToString("N");
            var previous = EditorSceneManager.GetActiveScene();
            var dirty = previous.isDirty;
            try
            {
                if (string.IsNullOrEmpty(previous.path))
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                var mapPath = folder + "/Map.vertexmesh";
                var scriptPath = folder + "/Rules.vsig";
                var scenePath = folder + "/Authoring.unity";
                var map = Fixture();
                File.WriteAllBytes(mapPath, map.Serialize());
                File.WriteAllText(scriptPath, "invalid unfinished statement\n");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Assert.That(VertexMapSceneBuilder.CreateSourceScene(mapPath, scriptPath, scenePath, compileScripts: false), Is.Not.Null);
                var saved = EditorSceneManager.OpenPreviewScene(scenePath);
                try
                {
                    Assert.That(saved.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VSigSceneRuntime>(true)), Is.Empty);
                    Assert.That(saved.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VertexObjectIdentity>(true)), Is.Not.Empty);
                    Assert.That(saved.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EntityMarker>(true)).Select(marker => marker.Guid),
                        Is.EquivalentTo(map.Containers.Select(container => container.Id)));
                }
                finally { EditorSceneManager.ClosePreviewScene(saved); }
                var bytes = File.ReadAllBytes(scenePath);
                Assert.Throws<InvalidDataException>(() => VertexMapSceneBuilder.CreateSourceScene(mapPath, scriptPath, scenePath));
                Assert.That(File.ReadAllBytes(scenePath), Is.EqualTo(bytes), "A failed compile must preserve the saved authoring scene.");
                if (!string.IsNullOrEmpty(previous.path))
                {
                    Assert.That(EditorSceneManager.GetActiveScene(), Is.EqualTo(previous));
                    Assert.That(previous.isDirty, Is.EqualTo(dirty));
                }
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void GeneratedScenePreservesAuthoredIdsOpenWorkAndLastGoodOutput()
        {
            var folder = "Assets/VertexSceneCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var scenePath = folder + "/Map.unity";
            var mapPath = folder + "/Map.vertexmesh";
            var scriptPath = folder + "/Rules.vsig";
            const string script = "ref door: func_door\nref lamp: env_light\non map.ready:\n    door.open()\n    lamp.off()\n";
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var map = Fixture();
                map.Containers = map.Containers.Concat(new[] { Entity("Lamp", "env_light", new(0, 2, 0)) }).ToArray();
                map.Environment = new MapEnvironment { SunColor = "#FF8800", SunIntensity = 2.5, SunShadows = false,
                    SunLighting = "Baked", FogMode = "Linear", FogStart = 4, FogEnd = 42, FogColor = "#223344",
                    AmbientColor = "#445566", ReflectionIntensity = .4, ReflectionBounces = 2 };
                File.WriteAllBytes(mapPath, map.Serialize());
                File.WriteAllText(scriptPath, script);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Assert.That(VertexMapSceneBuilder.CreateSourceScene(mapPath, scriptPath, scenePath), Is.Not.Null);
                Assert.That(EditorSceneManager.GetActiveScene().rootCount, Is.Zero);
                var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var markers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EntityMarker>()).ToArray();
                Assert.That(markers.Select(marker => marker.Guid), Is.EquivalentTo(map.Containers.Select(node => node.Id)));
                var lamp = markers.OfType<LightMarker>().Single();
                Assert.That(lamp.RequiresRuntimeEntity, Is.True, "VSig eligibility must survive scene save and reload.");
                Assert.That(lamp.GetComponent<Light>().lightmapBakeType, Is.EqualTo(LightmapBakeType.Realtime));
                Assert.That(RenderSettings.sun.intensity, Is.EqualTo(2.5));
                Assert.That(RenderSettings.sun.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(RenderSettings.sun.lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked));
                Assert.That(ColorUtility.ToHtmlStringRGB(RenderSettings.sun.color), Is.EqualTo("FF8800"));
                Assert.That(RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear, Is.True);
                Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(4));
                Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(42));
                Assert.That(ColorUtility.ToHtmlStringRGB(RenderSettings.ambientLight), Is.EqualTo("445566"));
                Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(.4f));
                Assert.That(RenderSettings.reflectionBounces, Is.EqualTo(2));
                var runtime = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VSigSceneRuntime>()).Single();
                Assert.That(runtime.MapId, Is.EqualTo(map.MapId));
                Assert.That(runtime.GetVertexProgram().ToProgram().Handlers.Single().Location.Path, Is.EqualTo(scriptPath));
                Assert.That(MapSceneUtility.TryGetContext(scene, out var context) && context.IsSceneContractSatisfied, Is.True);
                Assert.Throws<InvalidDataException>(() => VertexMapSceneBuilder.CreateSourceScene(mapPath, scriptPath, scenePath));

                var previous = File.ReadAllBytes(scenePath);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var unsaved = new GameObject("Unsaved work");
                Assert.Throws<InvalidDataException>(() => VertexMapSceneBuilder.CreateSourceScene(mapPath, scriptPath, scenePath));
                Assert.That(unsaved && unsaved.scene == scene, Is.True);
                Assert.That(File.ReadAllBytes(scenePath), Is.EqualTo(previous));
                Assert.That(EditorSceneManager.SaveScene(scene, folder + "/Workspace.unity"), Is.True);
                RenderSettings.fog = false;
                RenderSettings.ambientLight = Color.magenta;
                new GameObject("More unsaved work");
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(VertexMapSceneBuilder.CreateSourceScene(mapPath, scriptPath, scenePath), Is.Not.Null);
                Assert.That(EditorSceneManager.GetActiveScene(), Is.EqualTo(scene));
                Assert.That(RenderSettings.fog, Is.False, "Generating another scene must not change the open scene's fog.");
                Assert.That(RenderSettings.ambientLight, Is.EqualTo(Color.magenta));
                Assert.That(scene.isDirty && scene.rootCount == 2, Is.True);
                Assert.That(AssetDatabase.AssetPathToGUID(scenePath), Is.EqualTo(sceneGuid));

                previous = File.ReadAllBytes(scenePath);
                File.WriteAllText(scriptPath, "invalid statement\n");
                AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceSynchronousImport);
                var failure = Assert.Throws<InvalidDataException>(() => VertexMapSceneBuilder.CreateSourceScene(mapPath, scriptPath, scenePath));
                Assert.That(failure.Message, Does.Contain(scriptPath));
                Assert.That(File.ReadAllBytes(scenePath), Is.EqualTo(previous));
                Assert.That(EditorSceneManager.GetActiveScene(), Is.EqualTo(scene));
                Assert.That(scene.isDirty && scene.rootCount == 2, Is.True);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        // Batch bridge from Vertex.Desktop.Checks; the input is authored by real editor commands.
        public static void CheckDesktopAuthoredMap()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-vertexAuthoredMap");
            if (index < 0 || index + 1 == args.Length) throw new InvalidDataException("Pass -vertexAuthoredMap with the authored.vertexmesh from Vertex.Desktop.Checks.");
            var map = MapAsset.Deserialize(File.ReadAllBytes(args[index + 1]));
            const string folder = "Assets/VertexDesktopAuthoringCheck";
            const string path = folder + "/authored.vertexmesh";
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "VertexDesktopAuthoringCheck");
            try
            {
                File.WriteAllBytes(path, map.Serialize());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null);
                var markers = prefab.GetComponentsInChildren<EntityMarker>(true);
                Assert.That(markers.Select(marker => marker.Guid), Is.EquivalentTo(map.Containers.Where(node => node.Entity != null).Select(node => node.Id)));
                Assert.That(markers.OfType<SpawnMarker>().Count(), Is.EqualTo(1));
                Assert.That(markers.OfType<TriggerMarker>().Single().GetComponentsInChildren<Collider>(true), Is.Empty);
                var entrance = markers.OfType<DoorMarker>().Single(marker => marker.GetComponent<VertexObjectIdentity>().AuthoredName == "entrance");
                var exit = markers.OfType<DoorMarker>().Single(marker => marker.GetComponent<VertexObjectIdentity>().AuthoredName == "exit");
                Assert.That(entrance.Guid, Is.Not.EqualTo(exit.Guid));
                Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(2), "Entity bounds must not produce render meshes");
                Assert.That(prefab.GetComponentsInChildren<BoxCollider>(true), Has.Length.EqualTo(2), "Only the two door brushes have blocking collision");
                foreach (var marker in markers.Where(marker => marker is DoorMarker || marker is SpawnMarker))
                {
                    var source = map.Containers.Single(node => node.Id == marker.Guid);
                    Assert.That((marker.transform.position - VertexMapBuilder.Position(source.Entity.Position, map.MetersPerUnit)).sqrMagnitude, Is.LessThan(0.000001));
                }
                var diagnostics = new List<VSigDiagnostic>();
                var program = VSigSceneCompiler.Parse("ref entrance: func_door\non map.ready:\n    entrance.open()\n", diagnostics, markers, "authored.vsig");
                VSigSceneCompiler.Validate(program, markers, diagnostics);
                Assert.That(diagnostics, Is.Empty);
                Assert.That(VSigSceneCompiler.ResolveAliases(program, markers, diagnostics)["entrance"], Is.EqualTo(new[] { entrance }));
                Debug.Log("PASS: Desktop-authored spawn, trigger and linked doors import with stable identities, owned geometry and VSig bindings.");
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void ImportsOwnedGeometryTypedFieldsAndReferencesWithoutChangingAuthoredWorldSpace()
        {
            var map = Fixture();
            using var result = VertexMapBuilder.Build(MapAsset.Deserialize(map.Serialize()), null);
            var identities = result.Root.GetComponentsInChildren<VertexObjectIdentity>(true).ToDictionary(value => value.ObjectId);
            var spawn = identities[map.Containers[0].Id].GetComponent<SpawnMarker>();
            var trigger = identities[map.Containers[1].Id].GetComponent<TriggerMarker>();
            var door = identities[map.Containers[2].Id].GetComponent<DoorMarker>();
            var linked = identities[map.Containers[3].Id].GetComponent<DoorMarker>();
            Assert.That(spawn.Guid, Is.EqualTo(map.Containers[0].Id));
            Assert.That(spawn.transform.position, Is.EqualTo(new Vector3(1, 2, -3)));
            Assert.That(spawn.StartDisabled, Is.True);
            Assert.That(trigger.transform.position, Is.EqualTo(new Vector3(0, 1, -4)));
            Assert.That(trigger.GetBoundsXYZ(), Is.EqualTo(new Vector3(4, 2, 6)));
            Assert.That(trigger.GetComponentsInChildren<Collider>(true), Is.Empty, "SDK runtime creates the trigger, not a blocking level collider");
            Assert.That(door.Speed, Is.EqualTo(9));
            Assert.That(door.Guid, Is.Not.EqualTo(linked.Guid));
            Assert.That(door.GetWorldOpenDirection().x, Is.EqualTo(-1).Within(0.00001));
            var geometry = identities[map.Objects[0].Id];
            Assert.That(geometry.transform.position, Is.EqualTo(new Vector3(5, 2, -3)));
            Assert.That(Quaternion.Angle(geometry.transform.rotation, Quaternion.identity), Is.LessThan(0.001));
            Assert.That((geometry.GetComponent<BoxCollider>().bounds.size - new Vector3(2, 4, 1)).sqrMagnitude, Is.LessThan(0.00000001));
            Assert.That((int)GameObjectUtility.GetStaticEditorFlags(geometry.gameObject), Is.Zero);
            var renderMesh = geometry.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(renderMesh.uv2.Length, Is.EqualTo(renderMesh.vertexCount).And.GreaterThan(0));
            Assert.That(geometry.GetComponent<MeshRenderer>().receiveGI, Is.EqualTo(ReceiveGI.LightProbes));
            Assert.That(geometry.GetComponent<MeshRenderer>().staticShadowCaster, Is.False);

            var markers = new EntityMarker[] { spawn, trigger, door, linked };
            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse("ref door: func_door\non map.ready:\n    door.open()\n", diagnostics, markers, "map.vsig");
            VSigSceneCompiler.Validate(program, markers, diagnostics);
            Assert.That(diagnostics, Is.Empty);
            Assert.That(VSigSceneCompiler.BuildAlias(door), Is.EqualTo("door"));
            Assert.That(VSigSceneCompiler.ResolveAliases(program, markers, diagnostics)["door"], Is.EqualTo(new[] { door }));
            door.ValidateVertexImport();
            Assert.That(door.Guid, Is.EqualTo(map.Containers[2].Id), "revalidation must not randomize a Vertex ID");
        }

        [Test]
        public void ReimportKeepsUnityFileIdsAndSerializedEntityReferences()
        {
            const string folder = "Assets/VertexEntityImportCheck";
            const string path = folder + "/map.vertexmesh";
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "VertexEntityImportCheck");
            try
            {
                var map = Fixture();
                File.WriteAllBytes(path, map.Serialize());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null);
                var original = FileIds(prefab);
                var dependency = AssetDatabase.GetAssetDependencyHash(path);
                try
                {
                    AssetDatabase.RegisterCustomDependency(VertexSdkRegistry.DependencyKey("com.vellocet.sdk"), Hash128.Compute("changed-sdk-binding"));
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    Assert.That(AssetDatabase.GetAssetDependencyHash(path), Is.Not.EqualTo(dependency), "SDK changes must invalidate imported maps");
                }
                finally
                {
                    VertexSdkRegistry.Register("com.vellocet.sdk", VertexEntityImporter.Import);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
                map.Containers[2].Name = "Renamed Door";
                Array.Reverse(map.Containers);
                File.WriteAllBytes(path, map.Serialize());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(FileIds(prefab), Is.EquivalentTo(original));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var markers = instance.GetComponentsInChildren<EntityMarker>(true);
                    foreach (var marker in markers) marker.ValidateVertexImport();
                    var door = markers.OfType<DoorMarker>().Single(value => value.GetComponent<VertexObjectIdentity>().AuthoredName == "Renamed Door");
                    Assert.That(markers.OfType<DoorMarker>().Single(value=>value!=door).Guid, Is.EqualTo(map.Containers.Single(value => value.Name == "Other").Id));
                    Assert.That(markers.All(marker => marker.Guid == marker.GetComponent<VertexObjectIdentity>().ObjectId), Is.True);
                }
                finally { Object.DestroyImmediate(instance); }
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void InvalidMetadataReferencesAndGeometryFailWithSourceIdentity()
        {
            var map = Fixture();
            map.Containers[2].Entity.Properties[0].Value = "fast";
            var error = Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null));
            Assert.That(error.Message, Does.Contain(map.Containers[2].Id.ToString()));
            map = Fixture(); map.Containers[2].Entity.Properties = new[] { new EntityPropertyValue { Id="linked_doors",Value=new object[] {Guid.NewGuid().ToString("D")} } };
            Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null));
            map = Fixture(); map.Objects[0].ParentId = map.Containers[1].Id;
            Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null));
            map = Fixture(); map.Containers[0].Entity.SdkId = "missing.sdk";
            Assert.Throws<InvalidDataException>(() => VertexMapBuilder.Build(map, null));
        }

        private static Dictionary<Guid, long> FileIds(GameObject prefab) => prefab.GetComponentsInChildren<EntityMarker>(true).ToDictionary(
            marker => marker.Guid, marker => { Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(marker, out _, out long id), Is.True); return id; });

        private static MapAsset Fixture()
        {
            var spawn = Entity("Spawn", "info_player_spawn", new(2, 4, 6));
            spawn.Entity.Properties = new[] { new EntityPropertyValue { Id = "start_disabled", Value = true } };
            var trigger = Entity("Trigger", "trigger_multiple", new(0, 4, 8));
            trigger.Entity.Size = new(8, 4, 12);
            var door = Entity("Door", "func_door", new(10, 4, 6));
            door.Entity.Rotation = new(0, Math.Sqrt(0.5), 0, Math.Sqrt(0.5));
            var other = Entity("Other", "func_door", new(20, 4, 6));
            door.Entity.Properties = new[] { new EntityPropertyValue { Id = "speed", Value = 9d } };
            return new MapAsset { MapId = Guid.NewGuid(), MetersPerUnit = 0.5, Containers = new[] { spawn, trigger, door, other },
                Objects = new[] { Geometry(door), Geometry(other) } };
        }

        private static MapContainer Entity(string name, string type, Vector3Data position) => new()
        { Id = Guid.NewGuid(), Name = name, Entity = new MapEntity { SdkId = "com.vellocet.sdk", TypeId = type, Position = position } };

        private static MapObject Geometry(MapContainer owner)
        {
            var center = owner.Entity.Position;
            MapVertex Vertex(double x, double y, double u, double v) => new()
            { Position = new(center.X + x, center.Y + y, center.Z + 1), Normal = new(0, 0, 1), U = u, V = v };
            return new MapObject
            {
                Id = Guid.NewGuid(), Name = "Door geometry", ParentId = owner.Id, Origin = center,
                Vertices = new[] { Vertex(-2, -4, 0, 0), Vertex(2, -4, 1, 0), Vertex(2, 4, 1, 1), Vertex(-2, 4, 0, 1) },
                Surfaces = new[] { new MapSurface { Material = "builtin:checker", Indices = new[] { 0, 1, 2, 0, 2, 3 } } },
                Lightmap = new MapLightmap { GenerateUV2 = true, ResolutionScale = 1 },
                Colliders = new[] { new MapCollider { Kind = "Box", Center = center, Size = new(4, 8, 2) } }
            };
        }
    }
}
