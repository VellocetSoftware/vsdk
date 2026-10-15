using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Editor.VSig;
using Vertex.Unity;
using Vertex.Contracts;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>Updates generated map content while retaining saved Unity authoring.</summary>
    public static class VertexMapSceneBuilder
    {
        public static SceneAsset CreateSourceScene(string mapAssetPath, string scriptAssetPath, string scenePath,
            SdkResourceSnapshot[] resources = null, bool compileScripts = true)
        {
            if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.StartsWith("Assets/", StringComparison.Ordinal) || !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                scenePath.Contains("\\") || Array.Exists(scenePath.Split('/'), part => part is "." or ".." or "") ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(scenePath)?.Replace('\\', '/')))
                throw new InvalidDataException("The generated scene needs a .unity path in an existing Assets folder.");
            if (EditorSceneManager.GetSceneByPath(scenePath).isLoaded)
                throw new InvalidDataException("Close the authoring scene before updating it: " + scenePath);
            // SDK setup can change render-pipeline settings and invalidate model artifacts. Finish that import before reading the map.
            AssetDatabase.ImportAsset(mapAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(mapAssetPath);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(scriptAssetPath);
            if (!prefab || !prefab.TryGetComponent<VertexObjectIdentity>(out var identity) || identity.MapId == Guid.Empty)
                throw new InvalidDataException("Import a valid .vertexmesh map before creating its SDK scene.");
            var previous = EditorSceneManager.GetActiveScene();
            var emptyStartup = EditorSceneManager.sceneCount == 1 && previous.IsValid() &&
                string.IsNullOrEmpty(previous.path) && !previous.isDirty && previous.rootCount == 0;
            if (!emptyStartup)
                for (var i = 0; i < EditorSceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(EditorSceneManager.GetSceneAt(i).path))
                        throw new InvalidDataException("Save any untitled scene before generating a Vertex scene.");
            var existing = File.Exists(scenePath);
            var scene = existing
                ? EditorSceneManager.OpenScene(scenePath, emptyStartup ? OpenSceneMode.Single : OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, emptyStartup ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VertexObjectIdentity>(true))
                    .Where(item => item.ObjectId == item.MapId).ToArray();
                if (roots.Length > 1 || roots.Any(item => item.MapId != identity.MapId))
                    throw new InvalidDataException("The authoring scene contains another Vertex map. Use a separate scene for each map.");
                var instance = roots.SingleOrDefault()?.gameObject;
                if (instance && PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance) != prefab)
                    throw new InvalidDataException("The generated map prefab was unpacked or replaced. Restore its .vertexmesh prefab instance before saving from Vertex.");
                // Reimport updates the existing prefab's geometry and preserves scene object references and overrides.
                if (!instance) instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                // Stable importer names keep prefab file IDs intact; scene instances can show authored labels.
                foreach (var item in instance.GetComponentsInChildren<VertexObjectIdentity>(true))
                {
                    item.gameObject.name = item.AuthoredName;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item.gameObject);
                }
                var map = MapAsset.Deserialize(File.ReadAllBytes(mapAssetPath));
                var contentDirectory = Path.GetDirectoryName(mapAssetPath).Replace('\\', '/') + "/Content";
                var content = Directory.Exists(contentDirectory) ? Directory.GetFiles(contentDirectory, "*.vertexcontent", SearchOption.AllDirectories)
                    .Select(path => AssetDatabase.LoadAssetAtPath<VertexContentBindings>(path.Replace('\\', '/'))).Where(value => value).ToArray() : Array.Empty<VertexContentBindings>();
                var captured = instance.GetComponent<VertexMapResources>();
                if (resources is { Length: > 0 } || content.Length > 0)
                {
                    if (!captured) captured = instance.AddComponent<VertexMapResources>();
                    captured.Configure(map.MapId, resources ?? Array.Empty<SdkResourceSnapshot>());
                    captured.content = content;
                }
                else if (captured) UnityEngine.Object.DestroyImmediate(captured);
                VertexMapPolicy.Apply(map, instance, compiling: true);
                if (compileScripts && !VSigCompiledSceneInjector.InjectSource(scene, source, null, out var error))
                    throw new InvalidDataException(error);
                if (!compileScripts) VSigCompiledSceneInjector.RemoveExistingRuntimes(scene, default);
                if (!existing)
                {
                    var settings = MapSceneUtility.EnsureSceneContract(scene, false, out _);
                    var unityContent = new GameObject("Unity Content");
                    SceneManager.MoveGameObjectToScene(unityContent, scene);
                    if (settings.DirectionalLight) settings.DirectionalLight.transform.SetParent(unityContent.transform, true);
                    VertexMapEnvironment.Apply(scene, map.Environment);
                }
                // Validation and generation finish before replacing the last saved source scene.
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new IOException("Could not save the authoring scene: " + scenePath);
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            }
            finally
            {
                if (emptyStartup) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) EditorSceneManager.SetActiveScene(previous);
                }
            }
        }

        public static void CreateBatch()
        {
            var args = Environment.GetCommandLineArgs();
            string Required(string flag)
            {
                var index = Array.IndexOf(args, flag);
                if (index < 0 || index + 1 == args.Length) throw new ArgumentException("Missing argument " + flag);
                return args[index + 1];
            }
            var scene = CreateSourceScene(Required("-vertexMapAsset"), Required("-vertexScriptAsset"), Required("-vertexScene"));
            Debug.Log("VERTEX_SDK_SCENE_CREATED: " + AssetDatabase.GetAssetPath(scene));
        }
    }
}
