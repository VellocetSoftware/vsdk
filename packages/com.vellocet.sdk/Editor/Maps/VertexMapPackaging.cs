using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vellocet.Sdk.Editor.Addons;
using Vellocet.Sdk.Maps;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Maps
{
    public static class VertexMapPackaging
    {
        internal static AddonMapBuildRequest CreateRequest(BuildJob job, string lightmaps)
        {
            var profile = job.Profile switch
            {
                "Preview" => AddonMapBuildProfile.Development,
                "Release" => AddonMapBuildProfile.Release,
                _ => throw new InvalidDataException("Unsupported map build profile: " + job.Profile)
            };
            return new AddonMapBuildRequest
            {
                AddonId = job.RuntimeMapId, MapId = job.RuntimeMapId,
                AddonTitle = job.MapTitle, MapTitle = job.MapTitle,
                Profile = profile,
                Targets = profile == AddonMapBuildProfile.Release ? AddonMapBuildPolicy.ReleaseTargets :
                    new[] { (BuildTarget)Enum.Parse(typeof(BuildTarget), job.Target) },
                LightmapQuality = VertexBakePolicy.Quality(lightmaps), SkipLightingBake = lightmaps == "None"
            };
        }

        internal static PackageAsset[] PackageInventory(AddonMapBuildResult result)
        {
            var inventory = new List<PackageAsset>();
            var installed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var platform in result.SuccessfulPlatformBuilds)
            {
                if (platform.AssetInventory == null)
                    throw new InvalidDataException("Packaging returned without a bundle-membership audit for " + platform.Target);
                var prefix = Path.GetRelativePath(result.Layout.AddonRoot,
                    MapBuildArtifactPlanner.ComputePlatformOutputDirectory(result.Layout.MapRoot, platform.Target)).Replace('\\', '/') + "/";
                foreach (var asset in platform.AssetInventory)
                {
                    if (asset.Ownership is "base" or "dependency")
                    {
                        if (installed.Add(asset.Id)) inventory.Add(asset);
                        continue;
                    }
                    inventory.Add(new PackageAsset
                    {
                        Id = asset.Id, Source = asset.Source, Ownership = asset.Ownership, Fingerprint = asset.Fingerprint,
                        Address = asset.Address, Target = platform.Target.ToString(),
                        Bundles = asset.Bundles.Select(bundle => prefix + bundle).ToArray()
                    });
                }
            }
            return inventory.OrderBy(asset => asset.Target, StringComparer.Ordinal).ThenBy(asset => asset.Id, StringComparer.Ordinal).ToArray();
        }

        public static string Package(VertexBuildContext context, MapCompiler.Result compiled, VertexAssetCatalogProfile[] profiles,
            string[] resourceRequirements = null)
        {
            context.Report(.65, "Replacing installed base references and collecting reachable custom content…");
            var scene = EditorSceneManager.OpenScene(compiled.CompiledScenePath, OpenSceneMode.Single);
            var assets = new MapPackageAssets(profiles, AddressableAssetSettingsDefaultObject.GetSettings(true), context.Job.Content);
            assets.Prepare(scene);
            var ownedRuntime = compiled.RuntimeDependencies.Where(d => !assets.ReferenceInstalled(d.Guid, d.Address)).ToArray();
            var dependencies = assets.PlayerDependencies(new[] { scene.path }.Concat(ownedRuntime.Select(d => AssetDatabase.GUIDToAssetPath(d.Guid))));
            scene = EditorSceneManager.OpenScene(compiled.CompiledScenePath, OpenSceneMode.Single);
            var playerAssets = dependencies.Select(ObjectIdentifier.ToObject)
                .Where(value => value && EditorUtility.IsPersistent(value) && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(value))).Distinct().ToArray();
            if (!AddonScenePolicy.TryValidate(scene, ownedRuntime, out var error, playerAssets))
                throw new InvalidDataException("Packaged scene failed the SDK policy: " + error);
            compiled.PackageAssets = assets;
            compiled.Receipt.contentKind = MapCompilationReceipt.AddonContentKind;
            compiled.Receipt.compiledSceneHash = MapCompileSession.ComputeFileSha256(compiled.CompiledScenePath);
            compiled.Receipt.passes = compiled.Receipt.passes.Append(new MapCompilationReceipt.CompilePassRecord
            { name = "externalize-installed-assets", status = "ok", message = "Installed assets use runtime bindings; owned geometry keeps shared identity." }).ToArray();
            var project = ProjectProfile.Deserialize(ProfileFiles.Read(context.PathFor("VertexJob/Project.vertexproject")));
            // The SDK exports outside Unity projects. Keep its staging sibling private to this job,
            // then move only the verified package into the retained job output.
            var staging = context.Root + ".package-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            var request = CreateRequest(context.Job, compiled.Receipt.skipLightingBake ? "None" : compiled.Receipt.lightmapQuality);
            request.SceneAsset = compiled.CompiledScene; request.ScenePath = compiled.CompiledScenePath; request.Compiled = compiled;
            request.PackageOutputRoot = staging;
            request.RequiredAddonIds = (resourceRequirements ?? Array.Empty<string>())
                .Concat(VertexMediaPackaging.Requirements(context.Job, context.PathFor)).Distinct(StringComparer.Ordinal).ToArray();
            request.AddonTitle = request.MapTitle = context.Job.MapTitle ?? project.Name;
            request.AddonThumbnail = request.MapImage = VertexMapCompiler.LoadThumbnail(context.Job.Thumbnail);
            request.DefaultModeId = Constants.DefaultModeId; request.AddonVersion = "1";
            var result = AddonMapBuildService.Build(request,
                new DelegateMapBuildProgressSink((fraction, message) => context.Report(.67 + .28 * fraction, message), Debug.Log));
            context.CheckCanceled();
            if (!result.Success) throw new InvalidDataException(result.Error ?? "SDK map packaging failed.");
            var inventory = PackageInventory(result);
            var packageRoot = result.Layout.AddonRoot;
            var nativeInventory = VertexMediaPackaging.Write(context, packageRoot);
            inventory = inventory.Where(asset => asset.Ownership != "dependency" ||
                !asset.Address.StartsWith("asset:" + context.Job.RuntimeMapId + "/", StringComparison.Ordinal)).Concat(nativeInventory).ToArray();
            var manifest = new PackageManifest
            {
                JobId = context.Job.Id, ProjectId = context.Job.ProjectId, MapId = context.Job.MapId, Game = context.Job.Game,
                ContentId = context.Job.ContentId,
                Profile = context.Job.Profile, Target = context.Job.Target, InputsHash = context.Job.InputsHash,
                Format = "vellocet.addon/9", EntryPoint = Constants.AddonManifestFileName, Assets = inventory,
                Files = Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).Select(path => new BuildFile
                { Path = Path.GetRelativePath(packageRoot, path).Replace('\\', '/'), Bytes = new FileInfo(path).Length, Sha256 = MapCompileSession.ComputeFileSha256(path).ToLowerInvariant() }).ToArray()
            };
            manifest.Validate(context.Job);
            File.WriteAllBytes(Path.Combine(packageRoot, "vertex-package.json"), BuildProtocol.Serialize(manifest));
            var destination = context.PathFor("VertexPackage/" + Path.GetFileName(packageRoot));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            Directory.Move(packageRoot, destination);
            if (!Directory.EnumerateFileSystemEntries(staging).Any()) Directory.Delete(staging);
            context.Report(.96, $"Package verified: {inventory.Count(a => a.Ownership == "base")} installed references, " +
                $"{result.SuccessfulPlatformBuilds.Sum(platform => platform.BundlePaths.Length)} bundles across {result.SuccessfulPlatforms.Count} platforms; no duplicated assets within a platform.");
            return Path.GetRelativePath(context.Root, destination).Replace('\\', '/');
        }
    }
}
