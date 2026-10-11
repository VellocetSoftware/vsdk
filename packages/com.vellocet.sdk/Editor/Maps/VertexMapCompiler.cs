using Vertex.Unity;
using System;
using System.IO;
using System.Linq;
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
            var profiles = ValidateAuthoring(context.Job, context.PathFor);
            CompileValidated(context, profiles);
        }

        public static VertexAssetCatalogProfile[] ValidateAuthoring(BuildJob job, Func<string, string> pathFor)
        {
            VertexBakePolicy.Validate(job.Game.Baking);
            var catalog = VertexEntityCatalogExporter.Create();
            var authored = EntityCatalog.Deserialize(ProfileFiles.Read(pathFor("VertexJob/Catalogs/" + catalog.SdkId + ".vertexentities")));
            var installed = catalog.Serialize();
            if (!EntityContract(authored).SequenceEqual(EntityContract(catalog)))
            {
                File.WriteAllBytes(pathFor("VertexJob/installed.vertexentities"), installed);
                throw new InvalidDataException("The authoring entity catalog differs from the installed SDK. Export the game profile again and reload project resources.");
            }
            var profiles = AssetDatabase.FindAssets("t:VertexAssetCatalogProfile").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<VertexAssetCatalogProfile>).ToArray();
            foreach (var reference in job.Game.AssetCatalogs)
            {
                var matches = profiles.Where(p => p && p.catalogId == reference.Id && p.catalogVersion == reference.Version).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("Install exactly one matching asset profile: " + reference.Id + " " + reference.Version);
                matches[0].Validate();
            }
            var map = MapAsset.Deserialize(File.ReadAllBytes(pathFor(job.Map)));
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

        internal static byte[] EntityContract(EntityCatalog catalog)
        {
            // Display text and icons do not change how authored entities compile.
            catalog = EntityCatalog.Deserialize(catalog.Serialize());
            foreach (var entity in catalog.Entities)
            {
                entity.Name = entity.Id;
                entity.Category = "Entities";
                entity.IconPng = null;
                foreach (var property in entity.Properties)
                {
                    property.Name = property.Id;
                    property.Description = null;
                    property.Group = null;
                    foreach (var choice in property.Choices) choice.Name = choice.Id;
                }
                foreach (var warning in entity.Warnings ?? Array.Empty<EntityWarningDefinition>())
                    warning.Message = warning.Code;
            }
            foreach (var diagnostic in catalog.Diagnostics) diagnostic.Message = diagnostic.Code;
            return catalog.Serialize();
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

        private static void CompileValidated(VertexBuildContext context, VertexAssetCatalogProfile[] profiles)
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
                profiles.Where(p => context.Job.Game.AssetCatalogs.Any(reference => reference.Id == p.catalogId)).ToArray(),
                ResourceRequirements?.Invoke(resources) ?? Array.Empty<string>()) : null;
            context.Complete(outputPath, result.Receipt.passes.Select(pass => pass.name).Concat(package == null
                ? new[] { "validate-required-bakes" } : new[] { "validate-required-bakes", "audit-bundle-membership", "validate-package-integrity" }), package);
        }
    }
}
