using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.VSig.Runtime;
using Vertex.Contracts;
using Vertex.Unity;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class VertexMapSceneBuilderTests
    {
        private string _folder;
        private MapAsset _map;
        private SceneSetup[] _setup;
        private string MapPath => _folder + "/Map.vertexmesh";
        private string ScriptPath => _folder + "/Map.vsig";
        private string ScenePath => _folder + "/Authoring.unity";

        [SetUp]
        public void SetUp()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save open scenes before running scene build tests.");
            _setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _folder = "Assets/VertexSceneCheck_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folder));
            _map = new MapAsset
            {
                MapId = Guid.NewGuid(), MetersPerUnit = 1, Environment = new MapEnvironment(),
                Objects = new[] { Triangle("Floor"), Triangle("Removed later") }
            };
            WriteMap();
            WriteScript("# Geometry-only map\n");
        }

        [TearDown]
        public void TearDown()
        {
            if (_folder == null) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(_folder);
            if (_setup.Length > 0 && _setup.All(item => !string.IsNullOrEmpty(item.path)))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
            _folder = null;
        }

        [Test]
        public void RefreshPreservesPrefabsLightingReferencesAndSceneSettings()
        {
            Create();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var unityContent = scene.GetRootGameObjects().Single(root => root.name == "Unity Content");
            var floor = Identities(scene).Single(item => item.ObjectId == _map.Objects[0].Id);
            var floorRenderer = floor.GetComponent<MeshRenderer>();
            floorRenderer.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(floorRenderer);
            var authored = new GameObject("Lighting rig");
            var light = new GameObject("Accent", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(authored.transform, false);
            var inactive = new GameObject("Disabled decoration");
            inactive.transform.SetParent(authored.transform, false); inactive.SetActive(false);
            var prefabPath = _folder + "/Lighting.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(authored, prefabPath);
            Object.DestroyImmediate(authored);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            rig.name = "Lighting rig";
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            rig.transform.SetParent(unityContent.transform, false);
            light = rig.GetComponentInChildren<Light>();
            light.intensity = 13.5f; light.color = Color.magenta;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            var reference = new GameObject("Reference", typeof(MeshRenderer)).GetComponent<MeshRenderer>();
            SceneManager.MoveGameObjectToScene(reference.gameObject, scene);
            reference.probeAnchor = floor.transform;
            var sun = unityContent.GetComponentInChildren<Light>();
            sun.intensity = 2.75f;
            RenderSettings.fog = true; RenderSettings.fogDensity = .123f;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.cyan;
            var lighting = new LightingSettings { bakedGI = false, realtimeGI = false };
            var lightingPath = _folder + "/Lighting.asset";
            AssetDatabase.CreateAsset(lighting, lightingPath);
            Lightmapping.lightingSettings = lighting;
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            var lightId = GlobalObjectId.GetGlobalObjectIdSlow(light);
            var floorId = GlobalObjectId.GetGlobalObjectIdSlow(floor.gameObject);
            var sceneGuid = AssetDatabase.AssetPathToGUID(ScenePath);
            var prefabBytes = File.ReadAllBytes(prefabPath);
            Close();

            var removed = _map.Objects[1].Id;
            _map.Objects = new[] { Triangle("Updated floor", _map.Objects[0].Id, 9), Triangle("New wall") };
            _map.Environment.SunIntensity = 8;
            WriteMap();
            Create();
            Create();
            scene = EditorSceneManager.OpenScene(ScenePath);
            var identities = Identities(scene);
            floor = identities.Single(item => item.ObjectId == _map.Objects[0].Id);
            Assert.That(floor.name, Is.EqualTo("Updated floor"));
            Assert.That(floor.GetComponent<MeshRenderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off),
                "Updating authored labels must preserve overrides on the generated prefab.");
            Assert.That(floor.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, Is.EqualTo(9));
            Assert.That(identities.Any(item => item.ObjectId == removed), Is.False);
            Assert.That(identities.Count(item => item.ObjectId == _map.Objects[1].Id), Is.EqualTo(1));
            Assert.That(identities.Count(item => item.ObjectId == _map.MapId), Is.EqualTo(1));
            Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(floor.gameObject), Is.EqualTo(floorId));
            Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.EqualTo(sceneGuid));
            unityContent = scene.GetRootGameObjects().Single(root => root.name == "Unity Content");
            rig = unityContent.transform.Find("Lighting rig").gameObject;
            light = rig.GetComponentInChildren<Light>();
            Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(rig), Is.EqualTo(prefab));
            Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(light), Is.EqualTo(lightId));
            Assert.That(light.intensity, Is.EqualTo(13.5f));
            Assert.That(light.color, Is.EqualTo(Color.magenta));
            Assert.That(rig.transform.Find("Disabled decoration").gameObject.activeSelf, Is.False);
            Assert.That(File.ReadAllBytes(prefabPath), Is.EqualTo(prefabBytes));
            reference = scene.GetRootGameObjects().Single(root => root.name == "Reference").GetComponent<MeshRenderer>();
            Assert.That(reference.probeAnchor, Is.EqualTo(floor.transform));
            Assert.That(unityContent.GetComponentInChildren<Light>().intensity, Is.EqualTo(2.75f));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(.123f));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(Color.cyan));
            Assert.That(Lightmapping.lightingSettings, Is.EqualTo(AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingPath)));
            Close();

            var freshPath = _folder + "/Fresh.unity";
            VertexMapSceneBuilder.CreateSourceScene(MapPath, ScriptPath, freshPath);
            scene = EditorSceneManager.OpenScene(freshPath);
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>()).Single().intensity,
                Is.EqualTo(8), "Fresh build scenes still use the current Vertex environment.");
        }

        [Test]
        public void RefreshReplacesResourcesAndClearsUncompiledScripts()
        {
            SdkResourceSnapshot Resource(byte value) => new SdkResourceSnapshot
            {
                Type = "test.resource", Manifest = "settings.json",
                Files = new[] { new SdkResourceSource { Path = "settings.json", Data = new[] { value } } }
            };
            WriteScript("var initialized: bool = false\nserver on map.ready:\n    initialized = true\n");
            Create(new[] { Resource(1) });
            Create(new[] { Resource(2) });
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var root = Identities(scene).Single(item => item.ObjectId == _map.MapId);
            Assert.That(root.GetComponents<VertexMapResources>(), Has.Length.EqualTo(1));
            Assert.That(root.GetComponent<VertexMapResources>().Read().Single().Files.Single().Data, Is.EqualTo(new byte[] { 2 }));
            Assert.That(scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<VSigSceneRuntime>()).Count(), Is.EqualTo(1));
            Close();
            WriteScript("this is not valid VSig\n");
            Create(compileScripts: false);
            scene = EditorSceneManager.OpenScene(ScenePath);
            Assert.That(scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<VertexMapResources>()), Is.Empty);
            Assert.That(scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<VSigSceneRuntime>()), Is.Empty);
        }

        [Test]
        public void InvalidScriptPreservesSavedSceneAndRestoresActiveScene()
        {
            Create();
            var previous = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(previous, _folder + "/Other.unity"), Is.True);
            var bytes = File.ReadAllBytes(ScenePath);
            WriteScript("this is not valid VSig\n");
            Assert.Throws<InvalidDataException>(() => Create());
            Assert.That(File.ReadAllBytes(ScenePath), Is.EqualTo(bytes));
            Assert.That(EditorSceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
            Assert.That(EditorSceneManager.GetActiveScene(), Is.EqualTo(previous));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnpackedOrDuplicatedMapIsRejectedWithoutReplacingTheScene(bool duplicate)
        {
            Create();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var root = Identities(scene).Single(item => item.ObjectId == _map.MapId).gameObject;
            if (duplicate) PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MapPath), scene);
            else PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            Close();
            var bytes = File.ReadAllBytes(ScenePath);
            Assert.Throws<InvalidDataException>(() => Create());
            Assert.That(File.ReadAllBytes(ScenePath), Is.EqualTo(bytes));
        }

        [Test]
        public void DeletedMapInstanceIsRestoredWithoutDeletingUnityContent()
        {
            Create();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Object.DestroyImmediate(Identities(scene).Single(item => item.ObjectId == _map.MapId).gameObject);
            new GameObject("Unity addition");
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            Close();
            Create();
            scene = EditorSceneManager.OpenScene(ScenePath);
            Assert.That(Identities(scene).Count(item => item.ObjectId == _map.MapId), Is.EqualTo(1));
            Assert.That(scene.GetRootGameObjects().Count(root => root.name == "Unity addition"), Is.EqualTo(1));
        }

        private void Create(SdkResourceSnapshot[] resources = null, bool compileScripts = true) =>
            VertexMapSceneBuilder.CreateSourceScene(MapPath, ScriptPath, ScenePath, resources, compileScripts);

        private void WriteMap()
        {
            File.WriteAllBytes(MapPath, _map.Serialize());
            AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private void WriteScript(string text)
        {
            File.WriteAllText(ScriptPath, text);
            AssetDatabase.ImportAsset(ScriptPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        private static VertexObjectIdentity[] Identities(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<VertexObjectIdentity>(true)).ToArray();

        private static MapObject Triangle(string name, Guid? id = null, double width = 2) => new MapObject
        {
            Id = id ?? Guid.NewGuid(), Name = name, Lightmap = new MapLightmap { ResolutionScale = 1 },
            Vertices = new[]
            {
                new MapVertex { Position = new Vector3Data(0, 0, 0), Normal = new Vector3Data(0, 1, 0) },
                new MapVertex { Position = new Vector3Data(width, 0, 0), Normal = new Vector3Data(0, 1, 0), U = 1 },
                new MapVertex { Position = new Vector3Data(0, 0, 2), Normal = new Vector3Data(0, 1, 0), V = 1 }
            },
            Surfaces = new[] { new MapSurface { Material = "builtin:checker", Indices = new[] { 0, 2, 1 } } }
        };
    }
}
