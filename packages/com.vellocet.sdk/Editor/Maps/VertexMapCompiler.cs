using Vertex.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vellocet.Sdk.Editor.Entities;
using Vellocet.Sdk.Editor.Addons;
using Vellocet.Sdk.Maps;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Maps
{
    /// <summary>SDK policy for Vertex's retained, baked engine scenes. Distribution is a separate operation.</summary>
    public static class VertexMapCompiler
    {
        public static Func<SdkResourceSnapshot[], string[]> ResourceRequirements { get; set; }
        public static void Compile(VertexBuildContext context)
        {
            ValidateAuthoring(context.Job, context.PathFor);
            CompileValidated(context);
        }

        public static VertexAssetCatalogProfile[] ValidateAuthoring(BuildJob job, Func<string, string> pathFor)
        {
            VertexBakePolicy.Validate(job.Game.Baking);
            var map = MapAsset.Deserialize(File.ReadAllBytes(pathFor(job.Map)));
            var catalog = VertexEntityCatalogExporter.Create();
            var authored = EntityCatalog.Deserialize(ProfileFiles.Read(pathFor("VertexJob/Catalogs/" + catalog.SdkId + ".vertexentities")));
            if (EntityContractDifference(authored, catalog, map) is { } difference)
            {
                var installedPath = pathFor("VertexJob/installed.vertexentities");
                File.WriteAllBytes(installedPath, catalog.Serialize());
                throw new InvalidDataException("The authoring entity catalog differs from the installed SDK: " + difference +
                    ". Export the game profile from this Unity checkout and reload project resources in Vertex. Installed catalog: " + installedPath);
            }
            var profiles = LoadAssetProfiles(job);
            if (map.Objects.Any(mesh => mesh.Lightmap != null && (mesh.Lightmap.GenerateUV2 != job.Game.Baking.GenerateLightmapUVs || mesh.Lightmap.ResolutionScale != job.Game.Baking.LightmapScale)))
                throw new InvalidDataException("Map lightmap settings must match the game profile.");
            MapSettingsAuthoring.Read(map);
            if (map.MapId != job.MapId) throw new InvalidDataException("The map identity does not match the build request.");
            if (job.Package)
            {
                var request = VertexMapPackaging.CreateRequest(job, VertexBakePolicy.Lightmaps(map, job.Game.Baking));
                if (request.Profile == AddonMapBuildProfile.Release && !AddonMapBuildPolicy.TryValidateReleaseRequest(request, out var error))
                    throw new InvalidDataException(error);
            }
            var releaseArtwork = job.Package && job.Profile == "Release";
            if (!AddonThumbnailBuildUtility.TryValidateFile(job.Thumbnail == null ? null : pathFor(job.Thumbnail),
                    false, releaseArtwork, out var thumbnailError))
                throw new InvalidDataException(thumbnailError);
            return profiles;
        }

        internal static VertexAssetCatalogProfile[] LoadAssetProfiles(BuildJob job)
        {
            var profiles = AssetDatabase.FindAssets("t:VertexAssetCatalogProfile").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<VertexAssetCatalogProfile>).ToArray();
            return job.Game.AssetCatalogs.Select(reference =>
            {
                var matches = profiles.Where(p => p && p.catalogId == reference.Id && p.catalogVersion == reference.Version).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("Install exactly one matching asset profile: " + reference.Id + " " + reference.Version);
                matches[0].Validate();
                return matches[0];
            }).ToArray();
        }

        internal static string EntityContractDifference(EntityCatalog authored, EntityCatalog installed, MapAsset map)
        {
            var expected = JObject.Parse(Encoding.UTF8.GetString(EntityContract(authored)));
            var actual = JObject.Parse(Encoding.UTF8.GetString(EntityContract(installed)));
            foreach (var field in new[] { "formatVersion", "sdkId", "sdkVersion" })
                if (Difference(expected[field], actual[field], "Catalog " + field) is { } mismatch) return mismatch;
            var expectedTypes = Index(expected["entities"], item => (string)item["id"]);
            var actualTypes = Index(actual["entities"], item => (string)item["id"]);
            // Unused types may differ between a published SDK and a game checkout. Imported types must retain their contract.
            foreach (var id in map.Containers.Where(node => node.Entity?.SdkId == installed.SdkId)
                         .Select(node => node.Entity.TypeId).Distinct().OrderBy(id => id, StringComparer.Ordinal))
            {
                if (!expectedTypes.TryGetValue(id, out var before)) return "Entity '" + id + "' is missing from the authoring catalog";
                if (!actualTypes.TryGetValue(id, out var after)) return "Entity '" + id + "' is missing from the installed SDK";
                foreach (var members in new[] { "properties", "signals" })
                {
                    var mismatch = Members(before[members], after[members],
                        item => members == "properties" ? (string)item["id"] : item["kind"] + "." + item["name"],
                        "Entity '" + id + "' " + members);
                    if (mismatch != null) return mismatch;
                    ((JObject)before).Remove(members);
                    ((JObject)after).Remove(members);
                }
                if (Difference(before, after, "Entity '" + id + "'") is { } changed) return changed;
            }
            // Scripts can use globals without a placed entity, so retain every authored event signature.
            return Members(expected["globals"], actual["globals"], item => item["source"] + "." + item["name"], "Global event");

            static Dictionary<string, JToken> Index(JToken values, Func<JToken, string> key) =>
                values.ToDictionary(key, value => value, StringComparer.Ordinal);

            static string Members(JToken before, JToken after, Func<JToken, string> key, string label)
            {
                var available = Index(after, key);
                foreach (var member in before)
                {
                    var id = key(member);
                    if (!available.TryGetValue(id, out var value)) return label + " '" + id + "' is missing from the installed SDK";
                    if (Difference(member, value, label + " '" + id + "'") is { } mismatch) return mismatch;
                }
                return null;
            }

            static string Difference(JToken before, JToken after, string path)
            {
                if (JToken.DeepEquals(before, after)) return null;
                if (before is JObject first && after is JObject second)
                    foreach (var name in first.Properties().Select(p => p.Name).Union(second.Properties().Select(p => p.Name)))
                        if (Difference(first[name], second[name], path + "." + name) is { } mismatch) return mismatch;
                if (before is JArray a && after is JArray b)
                    for (var i = 0; i < Math.Max(a.Count, b.Count); i++)
                        if (Difference(i < a.Count ? a[i] : null, i < b.Count ? b[i] : null, path + "[" + i + "]") is { } mismatch) return mismatch;
                return path + " differs";
            }
        }

        internal static byte[] EntityContract(EntityCatalog catalog)
        {
            // Display metadata and declaration order do not change how authored entities compile.
            catalog = EntityCatalog.Deserialize(catalog.Serialize());
            catalog.Entities = catalog.Entities.OrderBy(entity => entity.Id, StringComparer.Ordinal).ToArray();
            catalog.Globals = catalog.Globals.OrderBy(signal => signal.Source, StringComparer.Ordinal)
                .ThenBy(signal => signal.Name, StringComparer.Ordinal).ToArray();
            catalog.Diagnostics = Array.Empty<EntityCatalogDiagnostic>();
            foreach (var entity in catalog.Entities)
            {
                entity.Name = entity.Id;
                entity.Category = "Entities";
                entity.IconPng = null;
                entity.Warnings = null;
                entity.Properties = entity.Properties.OrderBy(property => property.Id, StringComparer.Ordinal).ToArray();
                entity.Signals = entity.Signals.OrderBy(signal => signal.Kind).ThenBy(signal => signal.Name, StringComparer.Ordinal).ToArray();
                foreach (var property in entity.Properties) StripDisplayMetadata(property);
            }
            return catalog.Serialize();

            static void StripDisplayMetadata(EntityPropertyDefinition property)
            {
                property.Name = property.Id;
                property.Description = null;
                property.Group = null;
                property.VisibleWhen = null;
                property.Choices = property.Choices.OrderBy(choice => choice.Id, StringComparer.Ordinal).ToArray();
                if (property.Members != null)
                    property.Members = property.Members.OrderBy(member => member.Id, StringComparer.Ordinal).ToArray();
                foreach (var choice in property.Choices) choice.Name = choice.Id;
                foreach (var member in property.Members ?? Array.Empty<EntityPropertyDefinition>()) StripDisplayMetadata(member);
            }
        }

        public static Texture2D LoadThumbnail(string path)
        {
            if (path == null) return null;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidDataException("Could not import map thumbnail: " + path);
            // Captured inputs start with a GUID-only .meta; Unity can infer a cubemap from the image.
            if (importer.textureType != TextureImporterType.Default || importer.textureShape != TextureImporterShape.Texture2D ||
                importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                ?? throw new InvalidDataException("Could not load map thumbnail: " + path);
        }

        private static void CompileValidated(VertexBuildContext context)
        {
            var lightmaps = VertexBakePolicy.Lightmaps(MapAsset.Deserialize(File.ReadAllBytes(context.Job.Map)), context.Job.Game.Baking);
            context.Policy = "vellocet-sdk/vertex-compile-v1/" + context.Job.Profile;
            // Only lightmap baking is optional. Occlusion and spatialization always use the game policy.
            EditorUserBuildSettings.development = false;
            var sourcePath = UnityBuildProtocol.AssetRoot + "/Source/" + context.Job.RuntimeMapId + ".unity";
            var outputPath = UnityBuildProtocol.AssetRoot + "/Output/" + context.Job.RuntimeMapId + ".unity";
            context.Report(.04, "Creating the SDK scene from the captured map and VSig sources…");
            context.PrepareScenePath(sourcePath);
            var resources = VertexSdkRegistry.CaptureResources(context.Job, context.Root);
            var source = VertexMapSceneBuilder.CreateSourceScene(context.Job.Map, context.Job.Script, sourcePath, resources);
            context.PrepareScenePath(outputPath);
            var result = MapCompiler.Compile(source, context.Job.RuntimeMapId, context.Job.RuntimeMapId,
                MapCompilationReceipt.VertexContentKind, outputPath, new MapCompiler.CompileOptions
                {
                    LightmapQuality = VertexBakePolicy.Quality(lightmaps), BakePolicy = context.Job.Game.Baking,
                    RequireNonDevelopmentBuild = true,
                    SkipLightingBake = lightmaps == "None", SkipSpatialOctreeBake = false,
                    SyncBakedArtifactsToAuthoring = false, FinalizeLegacyGeometry = false,
                    ScriptSource = AssetDatabase.LoadAssetAtPath<TextAsset>(context.Job.Script),
                    ReportProgress = (fraction, message) => context.Report(.06 + (context.Job.Package ? .56 : .87) * fraction, message),
                    Log = Debug.Log
                });
            context.CheckCanceled();
            if (result.Receipt != null)
                File.WriteAllText(context.PathFor("VertexJob/sdk-receipt.json"), JsonUtility.ToJson(result.Receipt, true));
            if (!result.Success) throw new InvalidDataException(result.Error ?? "The SDK compiler did not complete.");
            context.Report(context.Job.Package ? .63 : .94, "Checking required navigation, lighting and occlusion artifacts…");
            var scene = EditorSceneManager.OpenScene(outputPath, OpenSceneMode.Single);
            if (!MapSceneUtility.HasBakedNavMesh(scene)) throw new InvalidDataException("Navigation bake produced no NavMesh data. Check the walkable geometry.");
            if (lightmaps != "None" && !MapSceneUtility.HasBakedLighting()) throw new InvalidDataException("Lighting bake produced no lighting data. Check the Unity build log and lighting setup.");
            if (!MapSceneUtility.HasBakedOcclusion()) throw new InvalidDataException("Occlusion bake produced no culling data. Check the map's static geometry and bounds.");
            if (result.Receipt.passes.Any(pass => pass.status != "ok")) throw new InvalidDataException("An SDK compilation pass failed.");
            var package = context.Job.Package ? VertexMapPackaging.Package(context, result,
                ResourceRequirements?.Invoke(resources) ?? Array.Empty<string>()) : null;
            context.Complete(outputPath, result.Receipt.passes.Select(pass => pass.name).Concat(package == null
                ? new[] { "validate-required-bakes" } : new[] { "validate-required-bakes", "audit-bundle-membership", "validate-package-integrity" }), package);
        }
    }
}
