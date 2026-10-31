using Vertex.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Maps;
using Vertex.Contracts;
using Vertex.Unity.Editor;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>Package ownership and actual SBP membership. Used only in an isolated compiler workspace.</summary>
    public sealed class MapPackageAssets
    {
        private readonly VertexAssetCatalogProfile[] _profiles;
        private readonly AddressableAssetSettings _settings;
        private readonly Dictionary<string, PackageAsset> _external = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PackageAsset> _extracted = new(StringComparer.Ordinal);
        private readonly Dictionary<Object, Object> _copies = new();
        private readonly HashSet<Object> _visited = new();
        private readonly Dictionary<Object, VertexContentBindings.Entry> _native = new();
        private readonly HashSet<string> _nativePaths = new(StringComparer.Ordinal);
        private readonly bool _externalContentOnly;
        private Dictionary<string, DateTime> _reports;
        private bool _generateLayout;
        private ProjectConfigData.ReportFileFormat _reportFormat;
        public PackageAsset[] Inventory { get; private set; }

        public MapPackageAssets(VertexAssetCatalogProfile[] profiles, AddressableAssetSettings settings, BuildContent[] content = null, bool externalContentOnly = false)
        {
            _profiles = profiles; _settings = settings;
            _externalContentOnly = externalContentOnly;
            foreach (var profile in profiles) profile.Validate();
            foreach (var catalog in (content ?? Array.Empty<BuildContent>()).Where(catalog => !externalContentOnly || catalog.External))
                foreach (var entry in VertexNativeContent.Entries(catalog.Path))
                {
                    // Prefer an asset's own readable key over a parent's internal dependency address.
                    if (!_native.ContainsKey(entry.asset) || string.IsNullOrEmpty(entry.subObject)) _native[entry.asset] = entry;
                    var path = AssetDatabase.GetAssetPath(entry.asset);
                    if (!path.EndsWith(".vertexcontent", StringComparison.Ordinal)) _nativePaths.Add(path);
                }
        }

        public string Ownership(string path)
        {
            if (_extracted.TryGetValue(path, out var source)) return source.Ownership;
            if (_nativePaths.Contains(path)) return "dependency";
            if (path == VertexDeveloperTextureImporter.AssetPath) return "base";
            if (_externalContentOnly) return "generated";
            if (path.StartsWith(UnityBuildProtocol.AssetRoot + "/Output/", StringComparison.Ordinal)) return "generated";
            if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(MonoScript)) return "engine"; // Serialized type metadata, never executable code in a bundle.
            var rules = _profiles.Select(p => p.RuleFor(path)).Where(r => r != null).ToArray();
            if (rules.Select(r => r.ownership).Distinct().Count() > 1)
                throw new InvalidDataException("Catalogs disagree about ownership: " + path);
            if (rules.Length > 0) return rules[0].ownership;
            if (SharedAssetAddressUtility.IsSdkSharedAssetPath(path)) return "base";
            if (path.StartsWith("Packages/com.unity.", StringComparison.Ordinal) ||
                path.StartsWith("Packages/com.vellocet.sdk/Runtime/", StringComparison.Ordinal) ||
                path.StartsWith("Packages/com.vellocet.sdk/Assets/Settings/Lighting/", StringComparison.Ordinal) ||
                path.StartsWith("Packages/com.vellocet.vertex/Runtime/", StringComparison.Ordinal) ||
                path.StartsWith("Resources/unity_builtin_extra", StringComparison.Ordinal) ||
                path.StartsWith("Library/unity default resources", StringComparison.Ordinal)) return "engine";
            if (path.StartsWith(UnityBuildProtocol.AssetRoot + "/Input/", StringComparison.Ordinal)) return "generated";
            throw new InvalidDataException("Declare base/custom ownership for this reachable dependency in a game asset profile: " + path);
        }

        public bool ReferenceInstalled(string guid, string address)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Ownership(path) != "base") return false;
            var entry = _settings.FindAssetEntry(guid);
            if (entry == null || string.IsNullOrWhiteSpace(entry.address) ||
                address != entry.address && address != guid && address != path)
                throw new InvalidDataException("The installed game must publish this base address before a map can reference it: " + address + " (" + path + ")");
            var id = guid + ":" + AssetCatalog.Hash(Encoding.UTF8.GetBytes(address)).Substring(0, 16);
            _external[id] = new PackageAsset { Id = id, Source = path, Ownership = "base", Address = address, Fingerprint = Fingerprint(path) };
            return true;
        }

        public void Prepare(Scene scene)
        {
            MapSceneUtility.UnpackPrefabInstances(scene);
            var root = MapSpatialGraphArtifacts.EnsureRuntimeRoot(scene);
            var bindings = root.GetComponent<MapBinding>() ?? root.AddComponent<MapBinding>();
            foreach (var item in scene.GetRootGameObjects())
            {
                if (!_externalContentOnly)
                {
                    foreach (var label in item.GetComponentsInChildren<Vellocet.Sdk.Entities.LabelMarker>(true)) label.PrepareVertexPackage();
                    foreach (var particle in item.GetComponentsInChildren<Vellocet.Sdk.Entities.ParticleMarker>(true)) particle.PrepareVertexPackage();
                }
                foreach (var resource in item.GetComponentsInChildren<VertexMapResources>(true))
                    resource.content = _externalContentOnly ? resource.content.Where(content => content && !content.external).ToArray() : Array.Empty<VertexContentBindings>();
                foreach (var component in item.GetComponentsInChildren<Component>(true)) if (component && component != bindings) Visit(component);
            }
            SceneReference(RenderSettings.skybox, "RenderSettings.skybox", () => RenderSettings.skybox = null);
            SceneReference(RenderSettings.customReflectionTexture, "RenderSettings.customReflectionTexture", () => RenderSettings.customReflectionTexture = null);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save external asset bindings.");

            void SceneReference(Object value, string property, Action clear)
            {
                if (!value || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(value))) return;
                if (_native.ContainsKey(value) || Ownership(AssetDatabase.GetAssetPath(value)) == "base") { Bind(null, property, value); clear(); }
                else if (!_externalContentOnly) Visit(value);
            }
            void Bind(Object target, string property, Object value)
            {
                if (_native.TryGetValue(value, out var native))
                {
                    if (!MapBinding.TryAssign(target, property, value, apply: false)) throw new InvalidDataException("Unsupported native content binding: " + property);
                    bindings.AddEditorTarget(target, property, native.key, value.GetType(), native.subObject);
                    var id = VertexNativeContent.ReceiptId(native);
                    var sourcePath = AssetDatabase.GetAssetPath(value);
                    _external[id] = new PackageAsset { Id = id, Source = sourcePath, Ownership = "dependency", Address = native.key, Fingerprint = Fingerprint(sourcePath) };
                    return;
                }
                if (target is UnityEngine.VFX.VisualEffect && value is Texture && property.StartsWith("m_PropertySheet.", StringComparison.Ordinal))
                {
                    using var serialized = new SerializedObject(target);
                    var slot = serialized.FindProperty(property.Substring(0, property.LastIndexOf('.')));
                    var name = slot?.FindPropertyRelative("m_Name")?.stringValue;
                    if (!string.IsNullOrEmpty(name)) property = "vfx-texture:" + name;
                }
                if (!MapBinding.TryAssign(target, property, value, apply: false))
                    throw new InvalidDataException($"Cannot restore installed asset reference {target?.name ?? "scene"}.{property}. Use a supported material/model binding or an owned custom asset.");
                var path = AssetDatabase.GetAssetPath(value); var guid = AssetDatabase.AssetPathToGUID(path);
                var entry = _settings.FindAssetEntry(guid);
                var address = entry?.address ?? throw new InvalidDataException("Base asset is absent from the installed game's Addressables catalog: " + path);
                ReferenceInstalled(guid, address);
                var subObject = AssetDatabase.IsSubAsset(value) ? value.name : null;
                if (subObject != null && (subObject.Contains('[') || subObject.Contains(']') ||
                    AssetDatabase.LoadAllAssetsAtPath(path).Count(a => a && a.GetType() == value.GetType() && a.name == subObject) != 1))
                    throw new InvalidDataException("Base subassets need unique loadable names: " + path + " [" + subObject + "]");
                bindings.AddEditorTarget(target, property, address, value.GetType(), subObject);
            }
            Object Extract(Object value)
            {
                var path = AssetDatabase.GetAssetPath(value);
                if (value is not (Mesh or Material or Texture or Vellocet.Sdk.Audio.AddonAudioCue) || !AssetDatabase.IsSubAsset(value)) return value;
                if (_copies.TryGetValue(value, out var copy)) return copy;
                var ownership = Ownership(path);
                if (ownership is "base" or "engine" or "dependency") return value;
                // Native containers already serialize as one owned asset. Copying their
                // subassets duplicates them when the container's main object is also used.
                if (AssetDatabase.IsNativeAsset(value)) return value;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId);
                const string folder = UnityBuildProtocol.AssetRoot + "/Output/PackageAssets";
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(UnityBuildProtocol.AssetRoot + "/Output", "PackageAssets");
                var destination = folder + "/" + guid + "-" + localId + ".asset";
                copy = Object.Instantiate(value); copy.name = value.name;
                AssetDatabase.CreateAsset(copy, destination); _copies.Add(value, copy);
                _extracted[destination] = new PackageAsset { Id = guid + "." + localId, Source = path, Ownership = ownership, Fingerprint = Fingerprint(path) };
                return copy;
            }
            void Visit(Object value)
            {
                if (!value || !_visited.Add(value) || value is MapBinding or MonoScript or Mesh or Texture or Shader or AudioClip) return;
                if (_externalContentOnly && value is VertexMapResources) return;
                if (_visited.Count > 32768) throw new InvalidDataException("Package dependency graph exceeds 32,768 objects.");
                if (value is GameObject prefab)
                { foreach (var component in prefab.GetComponentsInChildren<Component>(true)) Visit(component); return; }
                if (value is Material material)
                {
                    var externalShader = material.shader && Ownership(AssetDatabase.GetAssetPath(material.shader)) == "base";
                    if (externalShader) Bind(material, "m_Shader", material.shader);
                    foreach (var property in material.GetTexturePropertyNames())
                    {
                        var texture = material.GetTexture(property); if (!texture) continue;
                        var path = AssetDatabase.GetAssetPath(texture); if (string.IsNullOrEmpty(path)) continue;
                        if (_native.ContainsKey(texture) || Ownership(path) == "base") { Bind(material, "texture:" + property, texture); material.SetTexture(property, null); }
                        else { var copy = Extract(texture); if (copy != texture) material.SetTexture(property, (Texture)copy); }
                    }
                    if (externalShader) material.shader = Shader.Find("Hidden/InternalErrorShader");
                    EditorUtility.SetDirty(material); return;
                }
                if (value is not (Component or ScriptableObject)) return;
                // Add-on packages carry one source file; base scenes embed it in the cue.
                if (!_externalContentOnly && value is Vellocet.Sdk.Audio.AddonAudioCue cue)
                { cue.ClearEmbeddedMedia(); EditorUtility.SetDirty(cue); }
                using var serialized = new SerializedObject(value);
                var propertyIterator = serialized.GetIterator(); var enterChildren = true;
                while (propertyIterator.Next(enterChildren))
                {
                    enterChildren = propertyIterator.propertyPath != "vertexProgram";
                    if (propertyIterator.propertyType != SerializedPropertyType.ObjectReference || propertyIterator.propertyPath == "m_Script") continue;
                    var reference = propertyIterator.objectReferenceValue;
                    if (!reference || reference is MonoScript) continue;
                    if (_externalContentOnly && !_native.ContainsKey(reference)) continue;
                    if (value is UnityEngine.VFX.VisualEffect && reference is Texture && propertyIterator.propertyPath.StartsWith("m_PropertySheet.", StringComparison.Ordinal))
                    {
                        var slot = serialized.FindProperty(propertyIterator.propertyPath.Substring(0, propertyIterator.propertyPath.LastIndexOf('.')));
                        if (slot?.FindPropertyRelative("m_Overridden")?.boolValue == false)
                        {
                            // Dormant cached values follow the installed graph's default, not a new texture override.
                            propertyIterator.objectReferenceValue = null;
                            continue;
                        }
                    }
                    // Hosted libraries have already been embedded in VSig bytecode. Runtime markers need identity, not editor source assets.
                    if (value is Vellocet.Sdk.Entities.LogicScriptMarker host && reference == host.Library)
                    { propertyIterator.objectReferenceValue = null; continue; }
                    var path = AssetDatabase.GetAssetPath(reference); if (string.IsNullOrEmpty(path)) continue;
                    if (_native.ContainsKey(reference) || Ownership(path) == "base")
                    { Bind(value, propertyIterator.propertyPath, reference); propertyIterator.objectReferenceValue = null; }
                    else
                    {
                        var extracted = Extract(reference);
                        if (extracted != reference) propertyIterator.objectReferenceValue = extracted;
                        Visit(extracted);
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public IEnumerable<string> PackagedDependencies(IEnumerable<string> roots)
        {
            // One explicit entry per owned file puts dependencies shared by scene and runtime assets in one assets bundle.
            foreach (var path in PlayerDependencies(roots).Select(id => AssetDatabase.GUIDToAssetPath(id.guid))
                         .Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p, StringComparer.Ordinal))
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(path) is { } type && (type == typeof(MonoScript) || type == typeof(SceneAsset))) continue;
                var ownership = Ownership(path);
                if (ownership == "custom" || ownership == "generated") yield return path;
            }
        }

        public ObjectIdentifier[] PlayerDependencies(IEnumerable<string> roots)
        {
            // AssetDatabase dependencies also include importer metadata and lighting source scenes.
            // Ask Unity for the objects a player actually serializes, using the same API as SBP.
            var target = EditorUserBuildSettings.activeBuildTarget;
            var objects = new HashSet<ObjectIdentifier>();
            foreach (var path in roots.Distinct().Where(p => Ownership(p) is not ("base" or "dependency")))
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SceneAsset))
                {
                    var settings = new BuildSettings { target = target, group = BuildPipeline.GetBuildTargetGroup(target) };
                    var scene = ContentBuildInterface.CalculatePlayerDependenciesForScene(path, settings, new BuildUsageTagSet());
                    objects.UnionWith(scene.referencedObjects);
                }
                else
                {
                    var included = ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(new GUID(AssetDatabase.AssetPathToGUID(path)), target);
                    objects.UnionWith(included);
                    objects.UnionWith(ContentBuildInterface.GetPlayerDependenciesForObjects(included, target, null));
                }
            }
            if (objects.Count > 32768) throw new InvalidDataException("Package dependency graph exceeds 32,768 objects.");
            // Resolve identities only after reopening the scene: dependency calculation destroys temporary objects.
            return objects.ToArray();
        }

        public void BeginBuild()
        {
            _reports = ProjectConfigData.BuildReportFilePaths.Distinct(StringComparer.Ordinal).ToDictionary(p => p, File.GetLastWriteTimeUtc, StringComparer.Ordinal);
            _generateLayout = ProjectConfigData.GenerateBuildLayout; _reportFormat = ProjectConfigData.BuildLayoutReportFileFormat;
            ProjectConfigData.GenerateBuildLayout = true; ProjectConfigData.BuildLayoutReportFileFormat = ProjectConfigData.ReportFileFormat.JSON;
        }
        public void EndBuild()
        { ProjectConfigData.GenerateBuildLayout = _generateLayout; ProjectConfigData.BuildLayoutReportFileFormat = _reportFormat; }

        public void Audit(AddressablesPlayerBuildResult result, string outputDirectory)
        {
            var reports = ProjectConfigData.BuildReportFilePaths.Distinct(StringComparer.Ordinal).Where(p => !_reports.TryGetValue(p, out var modified) || File.GetLastWriteTimeUtc(p) != modified).ToArray();
            if (reports.Length != 1) throw new InvalidDataException("Packaging requires exactly one fresh Unity build-layout report.");
            var layout = BuildLayout.Open(reports[0], readFullFile: true) ?? throw new InvalidDataException("Could not read the bundle build-layout report.");
            try
            {
                var bundles = layout.Groups.SelectMany(g => g.Bundles).Concat(layout.BuiltInBundles).Distinct().ToArray();
                var shipped = Directory.GetFiles(outputDirectory, "*.bundle").Select(Path.GetFileName).OrderBy(p => p).ToArray();
                if (bundles.Length == 0 || !bundles.Select(b => Path.GetFileName(b.Name)).OrderBy(p => p).SequenceEqual(shipped))
                    throw new InvalidDataException("The actual bundle files do not match Unity's build-layout report.");
                var assets = new Dictionary<string, PackageAsset>(StringComparer.Ordinal);
                var serializedObjects = new HashSet<string>(StringComparer.Ordinal);
                foreach (var bundle in bundles)
                {
                    var name = Path.GetFileName(bundle.Name); var path = Path.Combine(outputDirectory, name);
                    var size = (ulong)new FileInfo(path).Length;
                    if (size != bundle.FileSize)
                        throw new InvalidDataException($"Bundle size differs from its build report: {name} ({size} != {bundle.FileSize}).");
                    var receipt = result.AssetBundleBuildResults.SingleOrDefault(b => Path.GetFileName(b.FilePath) == name);
                    if (receipt == null || receipt.Hash != bundle.Hash.ToString() || receipt.Crc != bundle.CRC)
                        throw new InvalidDataException($"Bundle hash differs from its build report: {name} ({receipt?.Hash ?? "missing"} != {bundle.Hash}).");
                    // GetCRCForAssetBundle reads legacy .manifest files, which SBP does not produce.
                    if (MapCompileSession.ComputeFileSha256(path) != MapCompileSession.ComputeFileSha256(receipt.FilePath))
                        throw new InvalidDataException("Packaged bytes differ from the bundle produced by Unity: " + name);
                    foreach (var file in bundle.Files)
                    {
                        foreach (var asset in file.Assets) Record(asset.Guid, asset.AssetPath, name, asset.Objects);
                        foreach (var asset in file.OtherAssets) Record(asset.AssetGuid, asset.AssetPath, name, asset.Objects);
                    }
                }
                Inventory = assets.Values.Concat(_external.Values).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
                File.Copy(reports[0], Path.Combine(outputDirectory, "bundle-layout.json"), overwrite: false);

                void Record(string guid, string source, string bundle, IEnumerable<BuildLayout.ObjectData> objects)
                {
                    if (string.IsNullOrWhiteSpace(source)) throw new InvalidDataException("A serialized bundle dependency has no auditable source: " + guid);
                    var ownership = Ownership(source);
                    if (ownership is "base" or "dependency") throw new InvalidDataException("Forbidden installed-content duplication in bundle " + bundle + ": " + source);
                    var extracted = _extracted.GetValueOrDefault(source);
                    if (ownership != "engine")
                        foreach (var item in objects)
                            if (!serializedObjects.Add(extracted?.Id ?? guid + "." + item.LocalIdentifierInFile))
                                throw new InvalidDataException("Duplicated custom/generated object (including extracted subassets): " + source);
                    if (!assets.TryGetValue(source, out var asset))
                    {
                        asset = new PackageAsset { Id = extracted?.Id ?? (string.IsNullOrEmpty(guid) ? source : guid), Source = extracted?.Source ?? source,
                            Ownership = ownership, Fingerprint = extracted?.Fingerprint ?? Fingerprint(source) };
                        assets.Add(source, asset);
                    }
                    if (asset.Bundles.Contains(bundle)) throw new InvalidDataException("Asset data was serialized into multiple files in one bundle: " + source);
                    asset.Bundles = asset.Bundles.Append(bundle).ToArray();
                    if (ownership != "engine" && asset.Bundles.Length != 1) throw new InvalidDataException("Duplicated custom/generated dependency: " + source);
                }
            }
            finally { layout.Close(); }
        }

        private static string Fingerprint(string path) => AssetCatalog.Hash(Encoding.UTF8.GetBytes(path + ":" + AssetDatabase.GetAssetDependencyHash(path)));
    }
}
