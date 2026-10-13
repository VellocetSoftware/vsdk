using Vertex.Unity;
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Editor.Tools;
using Vellocet.Sdk.Maps;
using Vellocet.Sdk.Spatial;
using Vertex.Contracts;
using Vertex.Unity.Editor;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class MapPackageAssetsTests
    {
        [Test]
        public void AuthoredArrays_StillRejectMoreThan65536Elements()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var line = new GameObject("Oversized authored line", typeof(LineRenderer)).GetComponent<LineRenderer>();
                line.positionCount = 65537;
                Assert.That(AddonScenePolicy.TryValidate(scene, Array.Empty<MapCompiler.RuntimeDependency>(),
                    out var error, Array.Empty<Object>()), Is.False);
                Assert.That(error, Does.Contain("element limit"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Any(scene => scene.isActive && scene.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SpatialGraphs_RejectInvalidStructureAndExcessiveNodeCounts(bool oversized)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            SpatialOctreeAsset asset = null;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                asset = ScriptableObject.CreateInstance<SpatialOctreeAsset>();
                if (oversized)
                    typeof(SpatialOctreeAsset).GetField("nodes", System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic).SetValue(asset,
                        new SpatialOctreeNodeData[SpatialOctreeBakeSettings.Default.MaxBakedNodeCount + 1]);
                Assert.That(AddonScenePolicy.TryValidate(scene, Array.Empty<MapCompiler.RuntimeDependency>(),
                    out var error, new Object[] { asset }), Is.False);
                Assert.That(error, Does.Contain(oversized ? "Node budget exceeded" : "incomplete or inconsistent"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Any(scene => scene.isActive && scene.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        [Test]
        public void SceneSwitch_ReloadsPersistentCatalogAndMaterialPreviewProvidersForPackaging()
        {
            var root = "Assets/VertexCatalogLifetimeCheck-" + Guid.NewGuid().ToString("N");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
                var provider = ScriptableObject.CreateInstance<VertexPropertyMaterialPreview>();
                AssetDatabase.CreateAsset(provider, root + "/Preview.asset");
                var profile = ScriptableObject.CreateInstance<VertexAssetCatalogProfile>();
                profile.catalogId = "checks.catalog-lifetime";
                profile.folders = new[] { new VertexAssetFolder { folder = root, ownership = "base" } };
                profile.materialPreviews = new[] { new VertexMaterialPreviewRule
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit"), provider = provider
                } };
                AssetDatabase.CreateAsset(profile, root + "/Profile.asset");
                AssetDatabase.SaveAssets();
                var job = new BuildJob { Game = new GameProfile { AssetCatalogs = new[]
                {
                    new ProfileReference { Id = profile.catalogId, Version = profile.catalogVersion, Path = "Catalog.vertexassets" }
                } } };
                Assert.That(VertexMapCompiler.LoadAssetProfiles(job), Has.Length.EqualTo(1));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var scenePath = root + "/Compiled.unity";
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                EditorUtility.UnloadUnusedAssetsImmediate();

                var reloaded = VertexMapCompiler.LoadAssetProfiles(job);
                Assert.That(reloaded, Has.Length.EqualTo(1));
                Assert.That(reloaded[0].materialPreviews[0].provider, Is.Not.Null);
                Assert.That(EditorUtility.IsPersistent(reloaded[0].materialPreviews[0].provider), Is.True);
                Assert.DoesNotThrow(() => new MapPackageAssets(reloaded, null));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(root);
                if (setup.Any(scene => scene.isActive && scene.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        [Test]
        public void NestedPrefabSourcesAreNotDependenciesOfTheCompiledScene()
        {
            if (AssetDatabase.IsValidFolder(UnityBuildProtocol.AssetRoot)) Assert.Ignore("A package workspace is already in use.");
            var root = "Assets/VertexNestedPrefabCheck-" + Guid.NewGuid().ToString("N");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var previousSettings = AddressableAssetSettingsDefaultObject.Settings;
            VertexAssetCatalogProfile profile = null;
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
                AssetDatabase.CreateFolder("Assets", "VertexBuild");
                AssetDatabase.CreateFolder(UnityBuildProtocol.AssetRoot, "Output");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var source = new GameObject("Private source", typeof(BoxCollider));
                source.GetComponent<BoxCollider>().size = new Vector3(2, 3, 4);
                var prefabPath = root + "/Private.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
                Object.DestroyImmediate(source);
                var parent = new GameObject("Map group");
                parent.SetActive(false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(parent.transform, false);
                instance.transform.localPosition = new Vector3(5, 7, 11);
                var scenePath = UnityBuildProtocol.AssetRoot + "/Output/Nested.unity";
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(AssetDatabase.GetDependencies(scenePath, true), Has.Member(prefabPath));
                var settings = AddressableAssetSettings.Create(root + "/Settings", "Settings", true, true);
                profile = ScriptableObject.CreateInstance<VertexAssetCatalogProfile>();
                profile.folders = new[] { new VertexAssetFolder { folder = root, ownership = "custom" } };
                new MapPackageAssets(new[] { profile }, settings).Prepare(scene);
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(instance), Is.False);
                Assert.That(AssetDatabase.GetDependencies(scenePath, true), Has.No.Member(prefabPath));
                Assert.That(instance.transform.localPosition, Is.EqualTo(new Vector3(5, 7, 11)));
                Assert.That(instance.GetComponent<BoxCollider>().size, Is.EqualTo(new Vector3(2, 3, 4)));
                Assert.That(parent.activeSelf, Is.False);
                Assert.That(PrefabUtility.IsPartOfPrefabAsset(prefab), Is.True);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(UnityBuildProtocol.AssetRoot);
                AssetDatabase.DeleteAsset(root);
                AddressableAssetSettingsDefaultObject.Settings = previousSettings;
                if (profile) Object.DestroyImmediate(profile);
                if (setup.Any(scene => scene.isActive && scene.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        [Test]
        public void ImportedTerrain_PreservesBothTextureLayersThroughPackagingAndRuntimeBinding()
        {
            if (AssetDatabase.IsValidFolder(UnityBuildProtocol.AssetRoot)) Assert.Ignore("A package workspace is already in use.");
            var root = "Assets/VertexTerrainCheck-" + Guid.NewGuid().ToString("N");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var previousSettings = AddressableAssetSettingsDefaultObject.Settings;
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
                AssetDatabase.CreateFolder(root, "Base");
                AssetDatabase.CreateFolder("Assets", "VertexBuild");
                AssetDatabase.CreateFolder(UnityBuildProtocol.AssetRoot, "Input");
                AssetDatabase.CreateFolder(UnityBuildProtocol.AssetRoot, "Output");
                var textures = new[] { new Texture2D(2, 2), new Texture2D(2, 2) };
                var materials = new Material[2];
                for (var i = 0; i < 2; i++)
                {
                    textures[i].SetPixels(Enumerable.Repeat(i == 0 ? Color.red : Color.blue, 4).ToArray());
                    textures[i].Apply();
                    AssetDatabase.CreateAsset(textures[i], root + "/Base/Texture" + i + ".asset");
                    materials[i] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    materials[i].SetTexture("_BaseMap", textures[i]);
                    materials[i].SetTexture("_BumpMap", textures[i]);
                    materials[i].SetTextureScale("_BaseMap", new Vector2(i + 2, i + 3));
                    AssetDatabase.CreateAsset(materials[i], root + "/Base/Layer" + i + ".mat");
                }
                var profile = ScriptableObject.CreateInstance<VertexAssetCatalogProfile>();
                profile.catalogId = "checks.terrain";
                profile.folders = new[] { new VertexAssetFolder { folder = root + "/Base", ownership = "base" } };
                AssetDatabase.CreateAsset(profile, root + "/Profile.asset");
                var settings = AddressableAssetSettings.Create(root + "/Settings", "Settings", false, true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
                var group = settings.CreateGroup("Installed", false, false, false, null, typeof(BundledAssetGroupSchema));
                foreach (var texture in textures)
                    settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture)), group).address = texture.name;
                AssetDatabase.SaveAssets();
                string Key(int i) => "asset:checks.terrain/" + VertexAssetCatalogExporter.NativeId(materials[i]);
                var map = new MapAsset
                {
                    MapId = Guid.NewGuid(), MetersPerUnit = 1, Objects = new[] { new MapObject
                    {
                        Id = Guid.NewGuid(), Name = "Terrain", Lightmap = new MapLightmap { ResolutionScale = 1 },
                        Vertices = new[]
                        {
                            new MapVertex { Position = new(0, 0, 0), Normal = new(0, 1, 0), Blend = 0 },
                            new MapVertex { Position = new(0, 0, 1), Normal = new(0, 1, 0), V = 1, Blend = .5 },
                            new MapVertex { Position = new(1, 0, 0), Normal = new(0, 1, 0), U = 1, Blend = 1 }
                        },
                        Surfaces = new[] { new MapSurface { Material = Key(0), BlendMaterial = Key(1), Indices = new[] { 0, 1, 2 } } }
                    } }
                };
                var mapPath = UnityBuildProtocol.AssetRoot + "/Input/Terrain.vertexmesh";
                File.WriteAllBytes(mapPath, map.Serialize());
                AssetDatabase.ImportAsset(mapPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(mapPath);
                Assert.That(prefab, Is.Not.Null);
                var imported = prefab.GetComponentInChildren<Renderer>().sharedMaterial;
                Assert.That(imported.GetTexture("_BaseMap"), Is.EqualTo(textures[0]), "Import must retain the base texture.");
                Assert.That(imported.GetTexture("_BlendMap"), Is.EqualTo(textures[1]), "Import must retain the paint texture.");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var scenePath = UnityBuildProtocol.AssetRoot + "/Output/Terrain.unity";
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                new MapPackageAssets(new[] { profile }, settings).Prepare(scene);
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var binding = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapBinding>()).Single();
                Assert.That(binding.Entries, Has.Count.EqualTo(4), "Both albedo and normal textures must be restored from the installed game.");
                var packed = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>()).Single().sharedMaterial;
                Assert.That(packed.GetTexture("_BaseMap"), Is.Null);
                Assert.That(packed.GetTexture("_BlendMap"), Is.Null);
                foreach (var entry in binding.Entries)
                {
                    Assert.That(entry.target, Is.EqualTo(packed), "Bindings must reference the material used by the saved renderer.");
                    Assert.That(MapBinding.TryAssign(entry.target, entry.propertyPath, textures.Single(t => t.name == entry.key)), Is.True);
                }
                Assert.That(packed.GetTexture("_BaseMap"), Is.EqualTo(textures[0]));
                Assert.That(packed.GetTexture("_BlendMap"), Is.EqualTo(textures[1]));
                Assert.That(packed.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(2, 3)));
                Assert.That(packed.GetTextureScale("_BlendMap"), Is.EqualTo(new Vector2(3, 4)));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(UnityBuildProtocol.AssetRoot); AssetDatabase.DeleteAsset(root);
                AddressableAssetSettingsDefaultObject.Settings = previousSettings;
                if (setup.Any(s => s.isActive && s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        [Test]
        public void ActualBundles_ExternalizeBaseTextures_DeduplicateCustomDependencies_RejectLeaksAndDuplicates()
        {
            const string root = "Assets/VertexPackageChecks";
            if (AssetDatabase.IsValidFolder(root) || AssetDatabase.IsValidFolder(UnityBuildProtocol.AssetRoot)) Assert.Ignore("A package workspace is already in use.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var previousSettings = AddressableAssetSettingsDefaultObject.Settings;
            var previousHost = SdkHost.Context;
            var createdDefaultFolder = !AssetDatabase.IsValidFolder("Assets/AddressableAssetsData");
            var output = Path.GetFullPath("Library/VertexPackageChecks/" + Guid.NewGuid().ToString("N"));
            VertexAssetCatalogProfile profile = null;
            try
            {
                AssetDatabase.CreateFolder("Assets", "VertexPackageChecks");
                if (createdDefaultFolder) AssetDatabase.CreateFolder("Assets", "AddressableAssetsData");
                AssetDatabase.CreateFolder(root, "Base"); AssetDatabase.CreateFolder(root, "Custom");
                AssetDatabase.CreateFolder("Assets", "VertexBuild"); AssetDatabase.CreateFolder(UnityBuildProtocol.AssetRoot, "Output");
                var settings = AddressableAssetSettings.Create(root + "/Settings", "Settings", true, true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
                settings.profileSettings.SetValue(settings.activeProfileId, Constants.LocalBuildVar, output);
                settings.profileSettings.SetValue(settings.activeProfileId, Constants.LocalLoadVar, output);
                var group = settings.CreateGroup("Package", false, false, false, null, typeof(BundledAssetGroupSchema));
                SdkHost.RegisterContext(new BaseHost());
                SharedAssetAddressableEditorUtility.EnsureSdkSharedAssets(settings);
                var baseGroup = SharedAssetAddressableEditorUtility.EnsureSharedGroup(settings);
                foreach (var item in settings.groups.Where(g => g && g.GetSchema<BundledAssetGroupSchema>()))
                    item.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = item == group;
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                schema.BuildPath.SetVariableByName(settings, Constants.LocalBuildVar);
                schema.LoadPath.SetVariableByName(settings, Constants.LocalLoadVar);

                var installed = new Texture2D(4, 4) { name = "Installed texture" }; installed.Apply();
                AssetDatabase.CreateAsset(installed, root + "/Base/Installed.asset");
                var shared = new Texture2D(4, 4) { name = "Shared custom texture" }; shared.Apply();
                AssetDatabase.CreateAsset(shared, root + "/Custom/Shared.asset");
                var unused = new Texture2D(4, 4) { name = "Unreachable texture" }; unused.Apply();
                AssetDatabase.CreateAsset(unused, root + "/Custom/Unused.asset");
                var first = new Material(Shader.Find("Standard")) { name = "First", mainTexture = shared };
                var second = new Material(Shader.Find("Standard")) { name = "Second", mainTexture = shared };
                second.SetTexture("_EmissionMap", installed);
                AssetDatabase.CreateAsset(first, root + "/Custom/First.mat"); AssetDatabase.CreateAsset(second, root + "/Custom/Second.mat");
                var installedGuid = AssetDatabase.AssetPathToGUID(root + "/Base/Installed.asset");
                settings.CreateOrMoveEntry(installedGuid, baseGroup).address = "installed/texture";
                var developer = VertexDeveloperTextureImporter.Resolve(DeveloperTexture.Find("builtin:dev-orange"));
                var developerAddress = SharedAssetAddressUtility.CreateAddress(VertexDeveloperTextureImporter.AssetPath);
                var developerEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(VertexDeveloperTextureImporter.AssetPath));
                Assert.That(developerEntry, Is.Not.Null, "The base game must publish developer materials automatically.");
                Assert.That(developerEntry.parentGroup, Is.EqualTo(baseGroup));
                Assert.That(developerEntry.address, Is.EqualTo(developerAddress));
                profile = ScriptableObject.CreateInstance<VertexAssetCatalogProfile>();
                profile.folders = new[] { new VertexAssetFolder { folder = root + "/Base", ownership = "base" },
                    new VertexAssetFolder { folder = root + "/Custom", ownership = "custom" } };
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var mesh = new Mesh { name = "Shared mesh", vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
                AssetDatabase.CreateAsset(mesh, root + "/Custom/Geometry.asset");
                foreach (var material in new[] { first, second, developer })
                { var go = new GameObject(material.name, typeof(MeshFilter), typeof(MeshRenderer)); go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<Renderer>().sharedMaterial = material; }
                var scenePath = UnityBuildProtocol.AssetRoot + "/Output/Map.unity";
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                var plan = new MapPackageAssets(new[] { profile }, settings); plan.Prepare(scene);
                Assert.That(second.GetTexture("_EmissionMap"), Is.Null, "A custom material must not pull its installed texture into the package.");
                var binding = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapBinding>()).Single();
                var textureBinding = binding.Entries.Single(entry => entry.key == "installed/texture");
                var developerBinding = binding.Entries.Single(entry => entry.key == developerAddress);
                Assert.That(developerBinding.subObject, Is.EqualTo(developer.name));
                Assert.That(((Renderer)developerBinding.target).sharedMaterial, Is.Null, "Maps carry a binding to the shared developer material, not a serialized copy.");
                Assert.That(MapBinding.TryAssign(second, textureBinding.propertyPath, installed), Is.True);
                Assert.That(second.GetTexture("_EmissionMap") == installed, Is.True, "The runtime assignment restores the original installed texture.");
                second.SetTexture("_EmissionMap", null); EditorUtility.SetDirty(second); AssetDatabase.SaveAssets();
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(scenePath), group).address = "map";
                foreach (var path in plan.PackagedDependencies(new[] { scenePath })) settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);

                BuildAndAudit(plan);
                Assert.That(plan.Inventory.Single(a => a.Source == root + "/Custom/Shared.asset").Bundles, Has.Length.EqualTo(1));
                Assert.That(plan.Inventory.Single(a => a.Source == root + "/Base/Installed.asset").Bundles, Is.Empty);
                Assert.That(plan.Inventory.Single(a => a.Source == VertexDeveloperTextureImporter.AssetPath).Bundles, Is.Empty,
                    "The map bundle must contain neither developer materials nor their textures.");
                Assert.That(plan.Inventory.Count(a => a.Source == root + "/Custom/Geometry.asset"), Is.EqualTo(1));
                Assert.That(plan.Inventory.Any(a => a.Source == root + "/Custom/Unused.asset"), Is.False, "Owning a folder does not bundle unused assets.");
                var mapBundles = Directory.GetFiles(output, "*.bundle");
                schema.IncludeInBuild = false;
                // Build only the installed assets exercised here, not the host game's entire shared catalog.
                foreach (var entry in baseGroup.entries.ToArray())
                    if (entry.guid != installedGuid && entry != developerEntry)
                        settings.RemoveAssetEntry(entry.guid);
                var installedSchema = baseGroup.GetSchema<BundledAssetGroupSchema>(); installedSchema.IncludeInBuild = true;
                installedSchema.BuildPath.SetVariableByName(settings, Constants.LocalBuildVar);
                installedSchema.LoadPath.SetVariableByName(settings, Constants.LocalLoadVar);
                settings.profileSettings.SetValue(settings.activeProfileId, Constants.LocalBuildVar, output + "/installed");
                var builtInNaming = settings.BuiltInBundleNaming;
                var monoScriptNaming = settings.MonoScriptBundleNaming;
                settings.BuiltInBundleNaming = BuiltInBundleNaming.Custom;
                settings.BuiltInBundleCustomNaming = "installed-test";
                settings.MonoScriptBundleNaming = MonoScriptBundleNaming.Custom;
                settings.MonoScriptBundleCustomNaming = "installed-test";
                AddressableAssetSettings.BuildPlayerContent(out var installedBuild);
                Assert.That(installedBuild.Error, Is.Null.Or.Empty);
                var loaded = mapBundles.Concat(installedBuild.AssetBundleBuildResults.Select(b => b.FilePath)).Select(AssetBundle.LoadFromFile).ToArray();
                try
                {
                    Assert.That(loaded.All(b => b), Is.True);
                    foreach (var bundle in loaded.Take(mapBundles.Length)) Assert.That(bundle.GetAllAssetNames().Any(p => p.Contains("/base/")), Is.False);
                    var packedFirst = Load<Material>(root + "/Custom/First.mat"); var packedSecond = Load<Material>(root + "/Custom/Second.mat");
                    var packedBase = Load<Texture2D>(root + "/Base/Installed.asset");
                    Assert.That(packedFirst.mainTexture && packedFirst.mainTexture == packedSecond.mainTexture, Is.True, "Two packed materials reuse the same loaded custom texture.");
                    Assert.That(packedSecond.GetTexture("_EmissionMap"), Is.Null);
                    Assert.That(MapBinding.TryAssign(packedSecond, "texture:_EmissionMap", packedBase), Is.True);
                    Assert.That(packedSecond.GetTexture("_EmissionMap") == packedBase, Is.True, "The packaged material uses the installed bundle's texture instance.");
                    var developerBundle = loaded.Single(b => b.GetAllAssetNames().Contains(VertexDeveloperTextureImporter.AssetPath, StringComparer.OrdinalIgnoreCase));
                    Assert.That(loaded.Take(mapBundles.Length).Contains(developerBundle), Is.False);
                    var packedDeveloper = developerBundle.LoadAssetWithSubAssets<Material>(VertexDeveloperTextureImporter.AssetPath)
                        .Single(material => material.name == developerBinding.subObject);
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    developerBinding = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapBinding>())
                        .SelectMany(item => item.Entries).Single(entry => entry.key == developerAddress);
                    Assert.That(MapBinding.TryAssign(developerBinding.target, developerBinding.propertyPath, packedDeveloper), Is.True);
                    Assert.That(((Renderer)developerBinding.target).sharedMaterial, Is.EqualTo(packedDeveloper));
                    Assert.That(packedDeveloper.mainTexture, Is.Not.Null, "The installed developer material retains its shared texture.");
                    T Load<T>(string path) where T : Object
                    {
                        var bundle = loaded.SingleOrDefault(b => b.GetAllAssetNames().Contains(path, StringComparer.OrdinalIgnoreCase));
                        Assert.That(bundle, Is.Not.Null, path + ": " + string.Join(", ", loaded.SelectMany(b => b.GetAllAssetNames())));
                        return bundle.LoadAsset<T>(path);
                    }
                }
                finally { foreach (var bundle in loaded.Reverse()) if (bundle) bundle.Unload(true); }
                schema.IncludeInBuild = true; installedSchema.IncludeInBuild = false;
                settings.BuiltInBundleNaming = builtInNaming;
                settings.MonoScriptBundleNaming = monoScriptNaming;
                settings.profileSettings.SetValue(settings.activeProfileId, Constants.LocalBuildVar, output);

                // A nested base leak is detected from the freshly built binary, even if a caller omits rewriting.
                second.SetTexture("_EmissionMap", installed); EditorUtility.SetDirty(second); AssetDatabase.SaveAssets();
                Assert.That(Assert.Throws<InvalidDataException>(() => BuildAndAudit(plan)).Message, Does.Contain("installed-content duplication"));
                second.SetTexture("_EmissionMap", null); EditorUtility.SetDirty(second); AssetDatabase.SaveAssets();
                // Deliberately split the custom roots and leave their shared texture implicit.
                settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(root + "/Custom/Shared.asset"));
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                Assert.That(Assert.Throws<InvalidDataException>(() => BuildAndAudit(plan)).Message, Does.Contain("Duplicated custom/generated"));

                void BuildAndAudit(MapPackageAssets assets)
                {
                    if (Directory.Exists(output)) Directory.Delete(output, true);
                    assets.BeginBuild(); AddressablesPlayerBuildResult result;
                    try { AddressableAssetSettings.BuildPlayerContent(out result); }
                    finally { assets.EndBuild(); }
                    Assert.That(result.Error, Is.Null.Or.Empty, "Actual Addressables content build must succeed before membership verification.");
                    Directory.CreateDirectory(output);
                    foreach (var bundle in result.AssetBundleBuildResults)
                    {
                        var destination = Path.Combine(output, Path.GetFileName(bundle.FilePath));
                        if (Path.GetFullPath(bundle.FilePath) != destination) File.Copy(bundle.FilePath, destination, true);
                    }
                    assets.Audit(result, output);
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AddressableAssetSettingsDefaultObject.Settings = previousSettings;
                SdkHost.RegisterContext(previousHost);
                if (createdDefaultFolder)
                { EditorBuildSettings.RemoveConfigObject(AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName); AssetDatabase.DeleteAsset("Assets/AddressableAssetsData"); }
                if (profile) Object.DestroyImmediate(profile);
                AssetDatabase.DeleteAsset(root); AssetDatabase.DeleteAsset(UnityBuildProtocol.AssetRoot);
                if (Directory.Exists(output)) Directory.Delete(output, true);
                if (setup.Any(s => s.isActive && s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private sealed class BaseHost : ISdkHostContext
        {
            public bool IsBaseProject => true;
            public string ModeName => "Base";
        }
    }
}
