using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class EditorConsistencyTests
    {
        private string _root;
        private SceneSetup[] _setup;

        [SetUp]
        public void SetUp()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                Assert.Ignore("Save open scenes before running scene tests.");
            _setup = EditorSceneManager.GetSceneManagerSetup();
            _root = "Assets/__EditorConsistency_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_root));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root == null) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(_root);
            if (_setup.Length > 0 && _setup.All(scene => !string.IsNullOrEmpty(scene.path) && File.Exists(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
        }

        [Test]
        public void CreationIsNativeAndActivationHappensOnlyOnce()
        {
            var attribute = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(typeof(MapDefinition), typeof(CreateAssetMenuAttribute));
            Assert.That(attribute.menuName, Is.EqualTo("Vellocet/Maps/Map Definition"));
            Assert.That(typeof(MapDefinition).GetField("defaultModeId"), Is.Null);
            var map = Create("MapDefinition.asset");
            Assert.That(map.Enabled, Is.False);
            Assert.That(map.IsDraft, Is.True);
            map.MarkCompiled();
            Assert.That(map.Enabled, Is.True);
            map.SetAvailability(false, true);
            map.MarkCompiled();
            Assert.That(map.Enabled, Is.False, "Recompilation must preserve explicit disabling.");
            Assert.That(map.EditorOnly, Is.True);
        }

        [Test]
        public void BaseProjectStatusComesFromRegisteredMapCapabilities()
        {
            var previous = Tools.SdkHost.Actions;
            try
            {
                Tools.SdkHost.Actions = null;
                Assert.That(Tools.SdkHost.IsBaseProject, Is.False);
                Tools.SdkHost.Actions = new Tools.SdkEditorActions();
                Assert.That(Tools.SdkHost.IsBaseProject, Is.False);
                Tools.SdkHost.Actions.BaseMaps = new BaseMapSettings();
                Assert.That(Tools.SdkHost.IsBaseProject, Is.True);
            }
            finally { Tools.SdkHost.Actions = previous; }
        }

        [TestCase(false, false, false, false)]
        [TestCase(false, true, false, false)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, true, false)]
        public void AvailabilityHasTheSameMeaningInEditorAndPlayers(bool enabled, bool editorOnly, bool editor, bool player)
        {
            var map = Create("MapDefinition.asset");
            map.MarkCompiled();
            map.SetAvailability(enabled, editorOnly);
            Assert.That(map.IsAvailable(true), Is.EqualTo(editor));
            Assert.That(map.IsAvailable(false), Is.EqualTo(player));
        }

        [Test]
        public void ValidationDoesNotRewriteDefinitionsOrReceipts()
        {
            var map = Create("MapDefinition.asset");
            var path = AssetDatabase.GetAssetPath(map);
            var before = File.ReadAllBytes(path);
            BaseMapAuthoring.ValidateAll();
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(Directory.Exists(_root + "/Compiled"), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledMapsPreserveBorrowedThumbnailOwnership(bool sdkOwned)
        {
            var map = Create("MapDefinition.asset");
            var definitionPath = AssetDatabase.GetAssetPath(map);
            var texturePath = sdkOwned
                ? AssetDatabase.FindAssets("t:Texture2D", new[] { Constants.PackageAssetsPath })
                    .Select(AssetDatabase.GUIDToAssetPath).First()
                : _root + "/SharedIcon.asset";
            if (!sdkOwned) AssetDatabase.CreateAsset(new Texture2D(1, 1), texturePath);
            var guid = AssetDatabase.AssetPathToGUID(texturePath);
            map = AssetDatabase.LoadAssetAtPath<MapDefinition>(definitionPath);
            map.MarkCompiled();
            map.SetAvailability(false, false);
            map.thumbnailReference = new UnityEngine.AddressableAssets.AssetReference(guid);
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
            var settings = UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.Create(_root + "/Settings", "Settings", true, true);
            var owner = settings.CreateGroup(sdkOwned ? Constants.BaseGroupName : "Game Icons", false, false, false, null,
                typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema),
                typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema));
            var entry = settings.CreateOrMoveEntry(guid, owner);
            var address = sdkOwned ? SharedAssetAddressUtility.CreateAddress(texturePath) : "inventory/icon";
            Assert.That(address, Is.Not.Null.And.Not.Empty);
            entry.address = address;
            var playerGroup = sdkOwned ? Constants.BaseGroupName : "Maps";
            map = AssetDatabase.LoadAssetAtPath<MapDefinition>(definitionPath);
            BaseMapPublication.Synchronize(settings, map, playerGroup, "definition", "scene");
            Assert.That(settings.FindAssetEntry(guid).parentGroup, Is.SameAs(owner));
            Assert.That(settings.FindAssetEntry(guid).address, Is.EqualTo(address));
            Assert.That(BaseMapPublication.Synchronize(settings, map, playerGroup, "definition", "scene"), Is.False);
        }

        [Test]
        public void ShippingGuardRemovesSourcesAndRejectsMapBuildScenes()
        {
            var map = Create("MapDefinition.asset");
            var definitionPath = AssetDatabase.GetAssetPath(map);
            var source = _root + "/Source.unity";
            var compiled = _root + "/Published.unity";
            var loose = _root + "/Loose.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), source);
            File.Copy(source, compiled);
            File.Copy(source, loose);
            AssetDatabase.ImportAsset(compiled, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(loose, ImportAssetOptions.ForceSynchronousImport);
            map = AssetDatabase.LoadAssetAtPath<MapDefinition>(definitionPath);
            map.authoringScenePath = source;
            map.compiledScenePath = compiled;
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
            var settings = UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.Create(_root + "/Settings", "Settings", true, true);
            foreach (var path in new[] { source, compiled, loose })
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.DefaultGroup);
            Assert.That(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(compiled)), Is.Not.Null);
            Assert.That(BaseMapShippingGuard.Normalize(settings, _root, persistChanges: false), Is.True);
            Assert.That(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(source)), Is.Null);
            Assert.That(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(loose)), Is.Null);
            Assert.That(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(compiled)), Is.Not.Null);
            var scenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(compiled, true) };
                var issues = new System.Collections.Generic.List<string>();
                BaseMapShippingGuard.ValidateBuild(issues, settings, _root);
                Assert.That(issues, Has.Count.EqualTo(1));
                Assert.That(issues[0], Does.Contain("must ship via compiled Addressables"));
            }
            finally { EditorBuildSettings.scenes = scenes; }
        }

        [Test]
        public void SharedOutputIsRejectedBeforeAnyCompilerWrite()
        {
            var map = Create("MapDefinition.asset");
            var other = Create("Other.asset");
            other.id += "_other";
            Assert.That(BaseMapAuthoring.TryValidateIdentityAndFolder(map, out var error), Is.False);
            Assert.That(error, Does.Contain("own folder"));
            Assert.That(Directory.Exists(_root + "/Compiled"), Is.False);
        }

        [Test]
        public void NestedDefinitionsAreProtectedFromParentCompilation()
        {
            var parent = Create("MapDefinition.asset");
            AssetDatabase.CreateFolder(_root, "Compiled");
            AssetDatabase.CreateFolder(_root + "/Compiled", "Child");
            var child = ScriptableObject.CreateInstance<MapDefinition>();
            child.id = parent.id + "_child";
            var childPath = _root + "/Compiled/Child/MapDefinition.asset";
            AssetDatabase.CreateAsset(child, childPath);
            Assert.That(BaseMapAuthoring.TryValidateIdentityAndFolder(parent, out var error), Is.False);
            Assert.That(error, Does.Contain("Compiled output"));
            Assert.That(BaseMapAuthoring.TryValidateIdentityAndFolder(child, out error), Is.False);
            Assert.That(error, Does.Contain("Compiled output"));
            Assert.That(File.Exists(childPath), Is.True);
        }

        [Test]
        public void MissingSourceIsADraftAndGeneratedScenesCannotBeSources()
        {
            var map = Create("MapDefinition.asset");
            Assert.That(BaseMapAuthoring.IsVertexOwned(map), Is.False);
            Assert.That(BaseMapAuthoring.TryValidateSource(map, out var error), Is.False);
            Assert.That(error, Is.EqualTo("Assign an Authoring Scene."));
            AssetDatabase.CreateFolder(_root, "Compiled");
            var path = _root + "/Compiled/Output.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), path);
            map = AssetDatabase.LoadAssetAtPath<MapDefinition>(_root + "/MapDefinition.asset");
            map.authoringScenePath = path;
            Assert.That(BaseMapAuthoring.TryValidateSource(map, out error), Is.False);
            Assert.That(error, Does.Contain("not compiled output"));
        }

        [TestCase("../escape")]
        [TestCase("unknown_example")]
        [TestCase("NM_example")]
        [TestCase("")]
        public void InvalidIdsCannotReachFileOperations(string id)
        {
            var map = Create("MapDefinition.asset");
            map.id = id;
            Assert.That(BaseMapAuthoring.TryValidateIdentityAndFolder(map, out _), Is.False);
        }

        [Test]
        public void EveryRegisteredPrefixResolvesTheSameWay()
        {
            foreach (var mode in ModeCatalog.All)
            {
                var id = mode.MapIdPrefix + "_example";
                Assert.That(SdkGame.Current.ResolveMapMode(id), Is.EqualTo(mode.Id));
                Assert.That(ModeCatalog.TryResolveMapMode(id, out var resolved), Is.True);
                Assert.That(resolved, Is.EqualTo(mode.Id));
                Assert.That(ModeCatalog.TryResolveMapMode(mode.MapIdPrefix + "_", out _), Is.False);
            }
        }

        [Test]
        public void MenuActionsHaveOneOwnerAndUseTheVellocetRoot()
        {
            var methods = TypeCache.GetMethodsWithAttribute<MenuItem>().Where(method =>
                method.DeclaringType != null && (method.DeclaringType.Assembly.GetName().Name.StartsWith("Vellocet.") ||
                method.DeclaringType.Assembly.GetName().Name.StartsWith("Grimwar.") ||
                method.DeclaringType.Assembly.GetName().Name == "Vertex.Unity.Editor"));
            var actions = methods.SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>())
                .Where(item => !item.validate).ToArray();
            Assert.That(actions.Where(item => item.menuItem.StartsWith("Tools/") && !item.menuItem.StartsWith("Tools/Vellocet/")), Is.Empty);
            Assert.That(actions.GroupBy(item => item.menuItem).Where(group => group.Count() > 1).Select(group => group.Key), Is.Empty);
            Assert.That(actions.Count(item => item.menuItem == "Tools/Vellocet/SDK/Open Vertex Map Editor…"), Is.EqualTo(1));
            Assert.That(actions.Count(item => item.menuItem == "CONTEXT/MapDefinition/Compile Map"), Is.EqualTo(1));
        }

        private MapDefinition Create(string name)
        {
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            map.id = ModeCatalog.All[0].MapIdPrefix + "_consistency_test";
            AssetDatabase.CreateAsset(map, _root + "/" + name);
            return map;
        }
    }
}
