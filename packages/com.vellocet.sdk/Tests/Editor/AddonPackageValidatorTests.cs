// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Editor.Addons;
using Vellocet.Sdk.Editor.Maps;
using Vellocet.Sdk.Maps;
using Vellocet.Sdk.Utilities;
using Vertex.Contracts;
using Vertex.Unity;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests
{
    public sealed partial class AddonPackageValidatorTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "vellocet-addon-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [Test]
        public void ManifestWriter_OmitsNullPayloadsAndTerminatesTheFile()
        {
            AddonHelper.WriteManifest(_root, new AddonManifest
            {
                schemaVersion = AddonManifest.CurrentSchemaVersion,
                gameId = SdkGame.Current.Id,
                audioRuntime = SdkGame.Current.AudioRuntimeCapability,
                id = "tests.audio",
                contents = new[]
                {
                    new AddonManifest.ContentEntry
                    {
                        kind = AddonManifest.AudioContentKind,
                        id = "tests_audio",
                        audio = new AddonManifest.AudioContent { catalog = "audio/catalog.json" }
                    }
                }
            });

            var json = File.ReadAllText(AddonHelper.GetManifestPath(_root));
            Assert.That(json, Does.Contain("\"audio\""));
            Assert.That(json, Does.Not.Contain("\"map\""));
            Assert.That(json, Does.Not.Contain("\"mod\""));
            Assert.That(json.EndsWith(Environment.NewLine, StringComparison.Ordinal), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [Test]
        public void VertexThumbnail_ImportsAsAnImageAndKeepsItsDimensions()
        {
            var path = "Assets/__VertexThumbnail_" + Guid.NewGuid().ToString("N") + ".png";
            var image = new Texture2D(1920, 1080);
            try
            {
                File.WriteAllBytes(path, image.EncodeToPNG());
                File.WriteAllText(path + ".meta", "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imported = VertexMapCompiler.LoadThumbnail(path);
                Assert.That(imported, Is.Not.Null);
                Assert.That(imported.width, Is.EqualTo(1920));
                Assert.That(imported.height, Is.EqualTo(1080));
                Assert.That(AssetDatabase.GetAssetPath(imported), Is.EqualTo(path));
            }
            finally
            {
                Object.DestroyImmediate(image);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void VertexRelease_BuildsAndAuditsEveryPlatform_PreviewKeepsSelectedTarget()
        {
            var mapId = ModeCatalog.ResolveMapIdPrefix(ModeCatalog.DefaultModeId) + "_developer";
            var game = new GameProfile
            {
                Id = SdkGame.Current.Id, Name = SdkGame.Current.DisplayName, Version = "1", EngineId = "unity",
                EngineVersion = Application.unityVersion, Build = UnityBuildProtocol.Configuration()
            };
            game.Build.PackageFormat = "vellocet.addon/9";
            game.Build.AllTargetsPackageProfiles = new[] { "Release" };
            var job = new BuildJob
            {
                Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), MapId = Guid.NewGuid(), Game = game,
                ContentId = mapId, MapTitle = "Developer Sandbox",
                Profile = "Release", Target = "StandaloneOSX", Package = true, InputsHash = new string('0', 64)
            };
            var release = VertexMapPackaging.CreateRequest(job, "Medium");
            Assert.That(release.MapId, Is.EqualTo(mapId));
            Assert.That(release.AddonId, Is.EqualTo(mapId));
            Assert.That(release.MapTitle, Is.EqualTo("Developer Sandbox"));
            var namedLayout = MapBuildArtifactPlanner.PlanAddon(_root, release.AddonId, release.MapId);
            Assert.That(namedLayout.AddonRoot, Is.EqualTo(Path.Combine(_root, mapId)));
            Assert.That(namedLayout.MapRoot, Is.EqualTo(Path.Combine(namedLayout.AddonRoot, Constants.MapsFolder, mapId)));
            Assert.That(release.Profile, Is.EqualTo(AddonMapBuildProfile.Release));
            Assert.That(release.Targets, Is.EquivalentTo(AddonMapBuildPolicy.ReleaseTargets));
            release.Targets = new[] { BuildTarget.StandaloneOSX };
            Assert.That(AddonMapBuildPolicy.TryValidateReleaseRequest(release, out var error), Is.False);
            Assert.That(error, Does.Contain("Windows Client, macOS Client, and Linux Dedicated Server"));

            var result = new AddonMapBuildResult
            {
                Layout = MapBuildArtifactPlanner.PlanAddon(_root, "checks.vertex", "map"),
                SuccessfulPlatformBuilds = AddonMapBuildPolicy.ReleaseTargets.Select(target => new Exporter.Result
                {
                    Target = target, Success = true, AssetInventory = new[]
                    {
                        new PackageAsset { Id = "installed", Source = "Base.mat", Ownership = "base", Address = "base/material", Fingerprint = job.InputsHash },
                        new PackageAsset { Id = "mesh", Source = "Mesh.asset", Ownership = "generated", Fingerprint = job.InputsHash, Bundles = new[] { "map.bundle" } }
                    }
                }).ToArray()
            };
            var inventory = VertexMapPackaging.PackageInventory(result);
            Assert.That(inventory.Count(asset => asset.Ownership == "base"), Is.EqualTo(1));
            Assert.That(inventory.Where(asset => asset.Ownership != "base").Select(asset => asset.Target),
                Is.EquivalentTo(AddonMapBuildPolicy.ReleaseTargets.Select(target => target.ToString())));
            var manifest = new PackageManifest
            {
                JobId = job.Id, ProjectId = job.ProjectId, MapId = job.MapId, Game = game, Profile = job.Profile,
                ContentId = job.ContentId,
                Target = job.Target, InputsHash = job.InputsHash, Format = game.Build.PackageFormat, EntryPoint = "addon.json", Assets = inventory,
                Files = inventory.SelectMany(asset => asset.Bundles).Append("addon.json").Select(path =>
                    new BuildFile { Path = path, Bytes = 0, Sha256 = job.InputsHash }).ToArray()
            };
            Assert.DoesNotThrow(() => manifest.Validate(job));
            manifest.Assets = inventory.Where(asset => asset.Target != "StandaloneLinux64").ToArray();
            Assert.That(Assert.Throws<InvalidDataException>(() => manifest.Validate(job)).Message, Does.Contain("Linux"));
            manifest.Assets = inventory.Append(inventory.First(asset => asset.Ownership != "base")).ToArray();
            Assert.Throws<InvalidDataException>(() => manifest.Validate(job));
            result.SuccessfulPlatformBuilds[0].AssetInventory = null;
            Assert.Throws<InvalidDataException>(() => VertexMapPackaging.PackageInventory(result));

            job.Profile = "Preview";
            var preview = VertexMapPackaging.CreateRequest(job, "None");
            Assert.That(preview.Profile, Is.EqualTo(AddonMapBuildProfile.Development));
            Assert.That(preview.Targets, Is.EqualTo(new[] { BuildTarget.StandaloneOSX }));
            Assert.That(preview.SkipLightingBake, Is.True);
        }

        [Test]
        public void ReleaseMatrix_RequiresAllPlatformsAndRoles()
        {
            var platforms = CreatePlatformDeclarations();

            Assert.That(AddonDistributionPolicy.TryValidateReleasePlatforms(platforms, out var error), Is.True,
                error);
            Assert.That(AddonDistributionPolicy.TryValidateReleasePlatforms(
                new[] { platforms[0], platforms[2] }, out error), Is.False);
            Assert.That(error, Does.Contain("exactly Windows Client"));

            platforms[2].role = AddonDistributionPolicy.ClientRole;
            Assert.That(AddonDistributionPolicy.TryValidateReleasePlatforms(platforms, out error), Is.False);
            Assert.That(error, Does.Contain(AddonDistributionPolicy.ServerRole));
        }

        [TestCase(MapLightmapQuality.Low)]
        [TestCase(MapLightmapQuality.Medium)]
        [TestCase(MapLightmapQuality.High)]
        public void ReleaseRequest_AllowsSelectedLightmapQuality(MapLightmapQuality lightmapQuality)
        {
            var request = new AddonMapBuildRequest
            {
                Targets = AddonMapBuildPolicy.ReleaseTargets,
                LightmapQuality = lightmapQuality,
                SkipLightingBake = false,
                SkipSpatialOctreeBake = false,
                ConfigureToolVisuals = true,
                PruneGeneratedMeshes = true
            };

            var targetsSupported = AddonMapBuildPolicy.AreReleaseTargetsSupported(out var missingTargets);
            var isValid = AddonMapBuildPolicy.TryValidateReleaseRequest(request, out var error);

            Assert.That(isValid, Is.EqualTo(targetsSupported), error);
            if (targetsSupported)
                Assert.That(error, Is.Null);
            else
                Assert.That(error, Does.Contain($"Missing: {missingTargets}"));
        }

        [Test]
        public void ReleaseRequest_RejectsSkippedSpatialOctreeBake()
        {
            var request = new AddonMapBuildRequest
            {
                Targets = AddonMapBuildPolicy.ReleaseTargets,
                SkipLightingBake = false,
                SkipSpatialOctreeBake = true,
                ConfigureToolVisuals = true,
                PruneGeneratedMeshes = true
            };

            Assert.That(AddonMapBuildPolicy.TryValidateReleaseRequest(request, out var error), Is.False);
            Assert.That(error, Does.Contain("spatial octree"));
        }

        [Test]
        public void ReleaseBuildConfiguration_RequiresNonDevelopmentMode()
        {
            var originalDevelopmentBuild = EditorUserBuildSettings.development;
            try
            {
                Assert.That(AddonMapBuildPolicy.RequiresNonDevelopmentBuild(AddonMapBuildProfile.Development),
                    Is.False);
                Assert.That(AddonMapBuildPolicy.RequiresNonDevelopmentBuild(AddonMapBuildProfile.Release), Is.True);

                Assert.That(AddonMapBuildService.TrySetDevelopmentBuild(true, out var error), Is.True, error);
                Assert.That(MapCompiler.TryValidateBuildConfiguration(new MapCompiler.CompileOptions(),
                    "Development map package build", out error), Is.True, error);
                var options = new MapCompiler.CompileOptions { RequireNonDevelopmentBuild = true };
                Assert.That(MapCompiler.TryValidateBuildConfiguration(options, "Release map package build", out error),
                    Is.False);
                Assert.That(error, Does.Contain("Development Build option"));

                Assert.That(AddonMapBuildService.TrySetDevelopmentBuild(false, out error), Is.True, error);
                Assert.That(MapCompiler.TryValidateBuildConfiguration(options, "Release map package build", out error),
                    Is.True, error);
            }
            finally
            {
                Assert.That(AddonMapBuildService.TrySetDevelopmentBuild(originalDevelopmentBuild, out var error),
                    Is.True, error);
            }
        }

        [Test]
        public void SkippedSpatialOctreeReceipt_IsValidWithoutGraphDependency()
        {
            const string mapId = "dm_skip_octree";
            var receipt = new MapCompilationReceipt
            {
                mapId = mapId,
                sceneAddress = mapId,
                skipSpatialOctreeBake = true,
                spatialGraphAddress = string.Empty,
                passes = new[]
                {
                    new MapCompilationReceipt.CompilePassRecord
                    {
                        name = "bake-spatial-octree",
                        status = "ok",
                        message = "Spatial octree bake was skipped."
                    }
                }
            };

            Assert.That(MapSpatialGraphArtifacts.TryValidateReceipt(receipt, mapId, mapId, out var message),
                Is.True, message);

            var result = new MapCompiler.Result { Receipt = receipt };
            Assert.That(MapSpatialGraphArtifacts.TryValidateCompiledResult(result, mapId, mapId, out message),
                Is.True, message);
            Assert.That(result.RuntimeDependencies, Is.Empty);
        }

        [Test]
        public void SkippedSpatialOctree_ClearsStaleSceneRuntimeDependency()
        {
            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            var runtimeRoot = new GameObject(MapSpatialGraphArtifacts.RuntimeRootName);
            try
            {
                SceneManager.MoveGameObjectToScene(runtimeRoot, scene);
                var dependencies = runtimeRoot.AddComponent<MapRuntimeDependencies>();
                var serializedDependencies = new SerializedObject(dependencies);
                var referenceProperty = serializedDependencies.FindProperty("spatialGraphReference");
                Assert.That(referenceProperty, Is.Not.Null);
                var guidProperty = referenceProperty.FindPropertyRelative("m_AssetGUID");
                Assert.That(guidProperty, Is.Not.Null);
                guidProperty.stringValue = "0123456789abcdef0123456789abcdef";
                serializedDependencies.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(MapSpatialGraphArtifacts.TryValidateSkippedSceneRuntimeDependency(scene,
                    out _), Is.False);
                Assert.That(MapSpatialGraphArtifacts.TryClearRuntimeDependency(scene, out var clearMessage),
                    Is.True, clearMessage);
                Assert.That(MapSpatialGraphArtifacts.TryValidateSkippedSceneRuntimeDependency(scene,
                    out var validationMessage), Is.True, validationMessage);
            }
            finally
            {
                Object.DestroyImmediate(runtimeRoot);
            }
        }

        [Test]
        public void DistributionMetadata_RejectsNonCurrentSchema()
        {
            var manifest = new AddonManifest
            {
                schemaVersion = AddonManifest.CurrentSchemaVersion - 1
            };

            Assert.That(AddonDistributionPolicy.TryValidateManifestMetadata(manifest, false, out var error),
                Is.False);
            Assert.That(error, Does.Contain($"Expected '{AddonManifest.CurrentSchemaVersion}'"));
        }

        [Test]
        public void CompilationReceipt_RejectsNonCurrentSchema()
        {
            var receipt = new MapCompilationReceipt
            {
                schemaVersion = MapCompilationReceipt.CurrentSchemaVersion + 1
            };

            Assert.That(MapCompilationReceiptUtility.ComputeSeal(receipt), Is.Empty);
            Assert.Throws<InvalidOperationException>(() => MapCompilationReceiptUtility.Seal(receipt));
            Assert.That(MapCompilationReceiptUtility.HasValidSeal(receipt), Is.False);
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase(" ", true)]
        [TestCase("Unassigned", true)]
        [TestCase("UnknownMusicSet", false)]
        public void MapPackages_AllowDefaultMusicAndRejectUnknownOverrides(string musicSet, bool valid)
        {
            WriteReleasePackage();
            Assert.That(AddonHelper.TryLoad(_root, out var manifest, out var loadError), Is.True, loadError);
            manifest.contents.Single(content => content.map != null).map.musicSet = musicSet;
            AddonHelper.WriteManifest(_root, manifest);
            Assert.That(AddonPackageValidator.TryValidate(_root, AddonPackageValidationMode.Release,
                out _, out var error), Is.EqualTo(valid), error);
            if (!valid) Assert.That(error, Does.Contain("music set"));
        }

        [Test]
        public void ReleasePackage_ValidatesCompleteOutput_AndRejectsTampering()
        {
            var tamperTarget = WriteReleasePackage();

            Assert.That(AddonPackageValidator.TryValidate(_root, AddonPackageValidationMode.Release,
                out var manifest, out var error), Is.True, error);
            Assert.That(manifest.distributionProfile, Is.EqualTo(AddonDistributionPolicy.ReleaseProfile));

            var tamperedBytes = File.ReadAllBytes(tamperTarget);
            Assert.That(tamperedBytes, Is.Not.Empty);
            tamperedBytes[0] = (byte)(tamperedBytes[0] ^ 0xff);
            File.WriteAllBytes(tamperTarget, tamperedBytes);

            Assert.That(AddonPackageValidator.TryValidate(_root, AddonPackageValidationMode.Release,
                out _, out error), Is.False);
            Assert.That(error, Does.Contain("does not match the SHA-256 recorded in addon.json"));
            Assert.That(error, Does.Contain("same completed build"));
        }

        [Test]
        public void ReleasePackage_RejectsSkippedSpatialOctreeReceipt()
        {
            WriteReleasePackage(true);

            Assert.That(AddonPackageValidator.TryValidate(_root, AddonPackageValidationMode.Release,
                out _, out var error), Is.False);
            Assert.That(error, Does.Contain("cannot skip the spatial octree bake"));
        }

        [Test]
        public void SharedIdentityPoliciesPreserveNamespacedIdsAndRejectAmbiguousSegments()
        {
            Assert.That(AddonIdPolicy.TryNormalize(" Community.Custom-Mode ", out var addonId), Is.True);
            Assert.That(addonId, Is.EqualTo("community.custom-mode"));
            Assert.That(ModeIdPolicy.TryNormalize("Community.Custom_Mode", out var modeId), Is.True);
            Assert.That(modeId, Is.EqualTo("community.custom_mode"));
            Assert.That(MapPrefixPolicy.TryNormalize("CM", out var prefix), Is.True);
            Assert.That(prefix, Is.EqualTo("cm"));

            Assert.That(AddonIdPolicy.TryNormalize(".community.escape", out _), Is.False);
            Assert.That(AddonIdPolicy.TryNormalize("community..escape", out _), Is.False);
            Assert.That(AddonIdPolicy.TryNormalize("community.-escape", out _), Is.False);
            Assert.That(MapPrefixPolicy.TryNormalize("cm_custom", out _), Is.False);
        }

        [Test]
        public void MapArtifactPlannerKeepsPackageModePrefixAndSlugIndependent()
        {
            Assert.That(MapBuildArtifactPlanner.NormalizeDefaultModeId("Community.Custom_Mode"),
                Is.EqualTo("community.custom_mode"));
            Assert.That(MapBuildArtifactPlanner.BuildMapId("CM", "Castle Run"),
                Is.EqualTo("cm_castle_run"));

            var layout = MapBuildArtifactPlanner.PlanAddon(_root, "Community.Custom-Mode", "cm_castle_run");
            Assert.That(layout.AddonRoot, Is.EqualTo(Path.Combine(_root, "community.custom-mode")));
            Assert.That(layout.MapRoot, Is.EqualTo(Path.Combine(
                _root, "community.custom-mode", Constants.MapsFolder, "cm_castle_run")));
        }

        [Test]
        public void PackagePublisher_PromotesStagedOutput_AndKeepsBackupOutOfTheAddonRoot()
        {
            var buildId = Guid.NewGuid().ToString("N");
            var finalAddonRoot = Path.Combine(_root, "dm_publish_test");
            var stagingRoot = Path.Combine(_root, ".dm_publish_test.staging-" + buildId);
            var stagingAddonRoot = Path.Combine(stagingRoot, "dm_publish_test");
            Directory.CreateDirectory(finalAddonRoot);
            Directory.CreateDirectory(stagingAddonRoot);
            File.WriteAllText(Path.Combine(finalAddonRoot, "version.txt"), "previous");
            File.WriteAllText(Path.Combine(stagingAddonRoot, "version.txt"), "next");

            Assert.That(AddonPackagePromoter.TryPromote(stagingAddonRoot, finalAddonRoot, buildId,
                out var error, out var preserveStaging), Is.True, error);
            Assert.That(preserveStaging, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(finalAddonRoot, "version.txt")), Is.EqualTo("next"));

            var backups = Directory.GetDirectories(stagingRoot, ".previous-*", SearchOption.TopDirectoryOnly);
            Assert.That(backups, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(backups[0], "version.txt")), Is.EqualTo("previous"));

            AddonPackagePromoter.CleanupStaging(stagingRoot, null);
            Assert.That(Directory.Exists(stagingRoot), Is.False);
        }

        private string WriteReleasePackage(bool skipSpatialOctreeBake = false)
        {
            const string mapId = "dm_release_policy_test";
            var sdkVersion = AddonManifestBuildUtility.ResolveSdkVersion();
            var buildId = Guid.NewGuid().ToString("N");
            var names = new[]
            {
                AddonDistributionPolicy.WindowsClientPlatform,
                AddonDistributionPolicy.MacOSClientPlatform,
                AddonDistributionPolicy.LinuxServerPlatform
            };
            var roles = new[]
            {
                AddonDistributionPolicy.ClientRole,
                AddonDistributionPolicy.ClientRole,
                AddonDistributionPolicy.ServerRole
            };
            var manifestPlatforms = new AddonManifest.PlatformCatalog[names.Length];
            var receiptPlatforms = new MapCompilationReceipt.PlatformArtifact[names.Length];
            string tamperTarget = null;

            for (var i = 0; i < names.Length; i++)
            {
                var relativeDirectory = $"{Constants.MapsFolder}/{mapId}/{names[i]}";
                var absoluteDirectory = Path.Combine(_root, relativeDirectory);
                Directory.CreateDirectory(absoluteDirectory);

                var catalog = WriteArtifact(relativeDirectory, "catalog.bin", "catalog-" + names[i]);
                var bundle = WriteArtifact(relativeDirectory, "content.bundle", "bundle-" + names[i]);
                tamperTarget ??= bundle.AbsolutePath;
                var files = new[] { catalog.ManifestFile, bundle.ManifestFile };

                manifestPlatforms[i] = new AddonManifest.PlatformCatalog
                {
                    name = names[i],
                    role = roles[i],
                    catalog = catalog.RelativePath,
                    files = files
                };
                receiptPlatforms[i] = new MapCompilationReceipt.PlatformArtifact
                {
                    name = names[i],
                    role = roles[i],
                    catalog = catalog.RelativePath,
                    files = new[]
                    {
                        ToReceiptFile(catalog.ManifestFile),
                        ToReceiptFile(bundle.ManifestFile)
                    }
                };
            }

            var receipt = new MapCompilationReceipt
            {
                schemaVersion = MapCompilationReceipt.CurrentSchemaVersion,
                mapId = mapId,
                mapTitle = "Release Policy Test",
                sceneAddress = mapId,
                contentKind = MapCompilationReceipt.AddonContentKind,
                distributionProfile = AddonDistributionPolicy.ReleaseProfile,
                distributionPolicy = AddonDistributionPolicy.ReleasePolicyV1,
                buildId = buildId,
                createdUtc = DateTime.UtcNow.ToString("o"),
                compilerVersion = sdkVersion,
                unityVersion = Application.unityVersion,
                skipSpatialOctreeBake = skipSpatialOctreeBake,
                platforms = receiptPlatforms
            };
            MapCompilationReceiptUtility.Seal(receipt);

            var receiptRelativePath =
                $"{Constants.MapsFolder}/{mapId}/{Constants.CompilationReceiptFileName}";
            File.WriteAllText(Path.Combine(_root, receiptRelativePath),
                MapCompilationReceiptUtility.Serialize(receipt));

            AddonHelper.WriteManifest(_root, new AddonManifest
            {
                schemaVersion = AddonManifest.CurrentSchemaVersion,
                gameId = SdkGame.Current.Id,
                audioRuntime = SdkGame.Current.AudioRuntimeCapability,
                id = mapId,
                title = "Release Policy Test",
                author = "Test",
                version = "1.0.0",
                distributionProfile = AddonDistributionPolicy.ReleaseProfile,
                distributionPolicy = AddonDistributionPolicy.ReleasePolicyV1,
                buildId = buildId,
                createdUtc = DateTime.UtcNow.ToString("o"),
                sdkVersion = sdkVersion,
                unityVersion = Application.unityVersion,
                contents = new[]
                {
                    new AddonManifest.ContentEntry
                    {
                        kind = AddonManifest.MapContentKind,
                        id = mapId,
                        title = "Release Policy Test",
                        map = new AddonManifest.MapContent
                        {
                            scene = mapId,
                            defaultModeId = SdkGame.Current.DefaultModeId,
                            musicSet = MapMusicSets.Name(default),
                            compiledReceipt = receiptRelativePath,
                            platforms = manifestPlatforms
                        }
                    }
                }
            });

            return tamperTarget;
        }

        private ArtifactFixture WriteArtifact(string relativeDirectory, string fileName, string contents)
        {
            var relativePath = $"{relativeDirectory}/{fileName}";
            var absolutePath = Path.Combine(_root, relativePath);
            File.WriteAllText(absolutePath, contents);
            Assert.That(SdkFileHashUtility.TryComputeSha256(absolutePath, out var sha256), Is.True);
            return new ArtifactFixture
            {
                AbsolutePath = absolutePath,
                RelativePath = relativePath,
                ManifestFile = new AddonManifest.PlatformFile
                {
                    path = relativePath,
                    size = new FileInfo(absolutePath).Length,
                    sha256 = sha256
                }
            };
        }

        private static AddonManifest.PlatformCatalog[] CreatePlatformDeclarations()
        {
            return new[]
            {
                new AddonManifest.PlatformCatalog
                {
                    name = AddonDistributionPolicy.WindowsClientPlatform,
                    role = AddonDistributionPolicy.ClientRole
                },
                new AddonManifest.PlatformCatalog
                {
                    name = AddonDistributionPolicy.MacOSClientPlatform,
                    role = AddonDistributionPolicy.ClientRole
                },
                new AddonManifest.PlatformCatalog
                {
                    name = AddonDistributionPolicy.LinuxServerPlatform,
                    role = AddonDistributionPolicy.ServerRole
                }
            };
        }

        private static MapCompilationReceipt.ArtifactFile ToReceiptFile(AddonManifest.PlatformFile file)
        {
            return new MapCompilationReceipt.ArtifactFile
            {
                path = file.path,
                size = file.size,
                sha256 = file.sha256
            };
        }

        private sealed class ArtifactFixture
        {
            internal string AbsolutePath;
            internal AddonManifest.PlatformFile ManifestFile;
            internal string RelativePath;
        }
    }
}
