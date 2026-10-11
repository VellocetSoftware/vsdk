#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vertex.Contracts;
using Vertex.Unity;
using Vertex.Unity.Editor;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class MediaContentTests
    {
        [Test]
        public void MapMedia_PackagesOneSoundAndKeepsProviderRequirements()
        {
            var root = "Assets/VertexMapMediaCheck_" + Guid.NewGuid().ToString("N");
            var output = Path.Combine(Path.GetTempPath(), "vertex-map-media-" + Guid.NewGuid().ToString("N"));
            try
            {
                const string relative = "Content/grimwar.audio/wind.wav";
                Directory.CreateDirectory(root + "/Content/grimwar.audio"); Directory.CreateDirectory(output);
                byte[] sound;
                using (var stream = new MemoryStream())
                {
                    using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(1636); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(1600); writer.Write(new byte[1600]); sound = stream.ToArray();
                }
                File.WriteAllBytes(root + "/" + relative, sound);
                var catalog = new ContentManifest { Id = "media_map", Title = "Media map", Requires = new[] { "shared_sounds" }, Assets = new[]
                { new ContentAsset { Id = "wind", Name = "Wind", Type = Vellocet.Sdk.Addons.AddonMedia.AudioType, Source = relative } },
                    Files = new[] { new BuildFile { Path = relative, Bytes = sound.Length, Sha256 = AssetCatalog.Hash(sound) } } };
                File.WriteAllBytes(root + "/content.json", catalog.Serialize());
                File.WriteAllBytes(root + "/content.vertexcontent", BuildProtocol.Serialize(new BuildContent { Path = root + "/content.json" }));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var job = new BuildJob { Profile = "Preview", Target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    Content = new[] { new BuildContent { Path = root + "/content.json" } } };
                var context = (VertexBuildContext)Activator.CreateInstance(typeof(VertexBuildContext),
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new object[] { Path.GetFullPath("."), job }, null);
                File.WriteAllText(output + "/addon.json", JsonUtility.ToJson(new Vellocet.Sdk.Addons.AddonManifest
                { id = catalog.Id, contents = Array.Empty<Vellocet.Sdk.Addons.AddonManifest.ContentEntry>() }));
                Assert.That(VertexMediaPackaging.Write(context, output), Is.Empty);
                var built = JsonUtility.FromJson<Vellocet.Sdk.Addons.AddonManifest>(File.ReadAllText(output + "/addon.json"));
                CollectionAssert.AreEquivalent(new[] { "media", "audio" }, built.contents.Select(entry => entry.kind));
                CollectionAssert.AreEqual(new[] { "shared_sounds" }, built.requires);
                Assert.That(Directory.GetFiles(output, "*.wav", SearchOption.AllDirectories), Has.Length.EqualTo(1));
                Assert.That(File.ReadAllText(output + "/audio.json"), Does.Contain("asset:media_map/wind"));
                CollectionAssert.AreEqual(sound, File.ReadAllBytes(output + "/" + relative));
            }
            finally { AssetDatabase.DeleteAsset(root); if (Directory.Exists(output)) Directory.Delete(output, true); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NativeMedia_ImportsBindsAndBuildsAnIndependentBundle(bool baseSave)
        {
            var root = "Assets/VertexMediaCheck_" + Guid.NewGuid().ToString("N");
            var output = Path.Combine(Path.GetTempPath(), "vertex-native-" + Guid.NewGuid().ToString("N"));
            var previous = EditorSceneManager.GetActiveScene();
            var scene = default(UnityEngine.SceneManagement.Scene);
            AssetBundle bundle = null;
            try
            {
                Directory.CreateDirectory(root + "/Content/unity.material");
                Directory.CreateDirectory(root + "/Content/unity.model");
                var image = new Texture2D(2, 2);
                File.WriteAllBytes(root + "/Content/unity.material/brick.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                File.WriteAllText(root + "/Content/unity.model/triangle.obj", "o triangle\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
                var catalog = new ContentManifest { Id = "shared_media", Title = "Shared media", Assets = new[]
                {
                    new ContentAsset { Id = "brick", Name = "Brick", Type = "unity.material", Source = "Content/unity.material/brick.png" },
                    new ContentAsset { Id = "triangle", Name = "Triangle", Type = "unity.model", Source = "Content/unity.model/triangle.obj" }
                } };
                File.WriteAllBytes(root + "/content.json", catalog.Serialize());
                File.WriteAllBytes(root + "/content.vertexcontent", BuildProtocol.Serialize(new BuildContent { Path = root + "/content.json", External = true }));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(root + "/content.vertexcontent", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var bindings = AssetDatabase.LoadAssetAtPath<VertexContentBindings>(root + "/content.vertexcontent");
                Assert.That(bindings, Is.Not.Null);
                var material = bindings.entries.Single(entry => entry.key == "asset:shared_media/brick").asset as Material;
                var model = bindings.entries.Single(entry => entry.key == "asset:shared_media/triangle").asset as GameObject;
                Assert.That(material && material.mainTexture && model, Is.True);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    string.IsNullOrEmpty(previous.path) ? NewSceneMode.Single : NewSceneMode.Additive);
                var item = new GameObject("Consumer", typeof(MeshFilter), typeof(MeshRenderer));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item, scene);
                item.GetComponent<MeshFilter>().sharedMesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
                item.GetComponent<MeshRenderer>().sharedMaterial = material;
                VertexMapResources resources = null;
                VertexContentBindings owned = null;
                if (baseSave)
                {
                    owned = ScriptableObject.CreateInstance<VertexContentBindings>();
                    AssetDatabase.CreateAsset(owned, root + "/owned.asset");
                    resources = item.AddComponent<VertexMapResources>(); resources.content = new[] { bindings, owned };
                }
                Assert.That(EditorSceneManager.SaveScene(scene, root + "/consumer.unity"), Is.True);
                new MapPackageAssets(Array.Empty<VertexAssetCatalogProfile>(), null,
                    new[] { new BuildContent { Path = root + "/content.json", External = true } }, externalContentOnly: baseSave).Prepare(scene);
                if (baseSave) CollectionAssert.AreEqual(new[] { owned }, resources.content, "Base scenes keep owned media and reference installed providers.");
                Assert.That(item.GetComponent<MeshFilter>().sharedMesh, Is.Null);
                Assert.That(item.GetComponent<MeshRenderer>().sharedMaterial, Is.Null);
                Assert.That(File.ReadAllText(root + "/consumer.unity"), Does.Contain("asset:shared_media/brick"));
                var platforms = VertexNativeContent.Build(root + "/content.json", output, new[] { EditorUserBuildSettings.activeBuildTarget.ToString() });
                Assert.That(platforms, Has.Length.EqualTo(1));
                bundle = AssetBundle.LoadFromFile(Path.Combine(output, platforms[0].Bundle));
                Assert.That(bundle, Is.Not.Null);
                var loaded = bundle.LoadAsset<VertexContentBindings>("asset:shared_media/brick");
                Assert.That(loaded.entries.Single(entry => entry.key == "asset:shared_media/brick" && string.IsNullOrEmpty(entry.subObject)).asset, Is.TypeOf<Material>());
                Assert.That(bundle.LoadAsset<VertexContentBindings>("asset:shared_media/triangle").entries.Any(entry => entry.asset is Mesh), Is.True, "Model meshes need stable shared bindings.");
                var previews = VertexNativeContent.Previews(root + "/content.json", output);
                Assert.That(previews.Length, Is.EqualTo(1));
                Assert.That(Vellocet.Sdk.Addons.AddonFilePolicy.TryValidateFileSize(previews[0].Preview.Path, previews[0].Preview.Bytes, out var error), Is.True, error);
                Assert.That(MapAsset.Deserialize(File.ReadAllBytes(Path.Combine(output, previews[0].Preview.Path))).Objects.Length, Is.GreaterThan(0));
            }
            finally
            {
                if (bundle) bundle.Unload(true);
                if (scene.IsValid() && scene.isLoaded)
                {
                    if (EditorSceneManager.sceneCount == 1) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    else EditorSceneManager.CloseScene(scene, true);
                }
                if (previous.IsValid() && previous.isLoaded) EditorSceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(root);
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        }
    }
}
#endif
