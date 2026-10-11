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
        [Test]
        public void GravewarCatalogUsesGrPrefixForNormalMapAuthoring()
        {
            Assert.That(ModeCatalog.TryGetByCanonicalId(ModeCatalog.GravewarId, out var descriptor), Is.True);
            Assert.That(descriptor.DisplayName, Is.EqualTo("Gravewar"));
            Assert.That(descriptor.MapIdPrefix, Is.EqualTo("gr"));
            Assert.That(ModeCatalog.ResolveMapIdPrefix(ModeCatalog.GravewarId), Is.EqualTo("gr"));
            Assert.That(MapBuildArtifactPlanner.ResolveSuggestedPrefix(ModeCatalog.GravewarId), Is.EqualTo("gr"));
            Assert.That(MapBuildArtifactPlanner.BuildCanonicalAddonMapId(ModeCatalog.GravewarId, "Crypt"),
                Is.EqualTo("gr_crypt"));
        }

        [Test]
        public void ModPackage_UsesTheSharedAddonManifestAndRejectsEscapingPaths()
        {
            var modDirectory = Path.Combine(_root, "mods", "community.hello");
            Directory.CreateDirectory(modDirectory);
            File.WriteAllText(Path.Combine(modDirectory, AddonManifest.ModManifestFileName), "{}");
            var manifest = new AddonManifest
            {
                schemaVersion = AddonManifest.CurrentSchemaVersion,
                id = "community.hello",
                title = "Hello Server",
                author = "Test",
                version = "1.0.0",
                distributionProfile = AddonDistributionPolicy.DevelopmentProfile,
                buildId = Guid.NewGuid().ToString("N"),
                createdUtc = DateTime.UtcNow.ToString("o"),
                sdkVersion = "test",
                unityVersion = Application.unityVersion,
                contents = new[]
                {
                    new AddonManifest.ContentEntry
                    {
                        kind = AddonManifest.ModContentKind,
                        id = "community.hello",
                        title = "Hello Server",
                        mod = new AddonManifest.ModContent
                        {
                            manifest = "mods/community.hello/mod.json"
                        }
                    }
                }
            };
            AddonHelper.WriteManifest(_root, manifest);

            Assert.That(AddonPackageValidator.TryValidate(_root, AddonPackageValidationMode.Standard,
                out _, out var error), Is.True, error);

            manifest.contents[0].mod.manifest = "../mod.json";
            AddonHelper.WriteManifest(_root, manifest);
            Assert.That(AddonPackageValidator.TryValidate(_root, AddonPackageValidationMode.Standard,
                out _, out error), Is.False);
            Assert.That(error, Does.Contain("invalid path"));
        }

        [Test]
        public void MapPackageStaging_ReplacesOnlySelectedMapContent()
        {
            var sourceRoot = Path.Combine(_root, "source");
            var destinationRoot = Path.Combine(_root, "staging");
            var modRoot = Path.Combine(sourceRoot, "mods", "community.hello");
            var replacedMapRoot = Path.Combine(sourceRoot, Constants.MapsFolder, "dm_replaced");
            var preservedMapRoot = Path.Combine(sourceRoot, Constants.MapsFolder, "dm_preserved");
            Directory.CreateDirectory(modRoot);
            Directory.CreateDirectory(replacedMapRoot);
            Directory.CreateDirectory(preservedMapRoot);
            File.WriteAllText(Path.Combine(modRoot, AddonManifest.ModManifestFileName), "{}");
            File.WriteAllText(Path.Combine(modRoot, "HelloMod.csx"), "public sealed class HelloMod {}");
            File.WriteAllText(Path.Combine(replacedMapRoot, "stale.bundle"), "stale");
            File.WriteAllText(Path.Combine(preservedMapRoot, "current.bundle"), "current");
            AddonHelper.WriteManifest(sourceRoot, new AddonManifest
            {
                schemaVersion = AddonManifest.CurrentSchemaVersion,
                id = "community.mixed",
                title = "Mixed Addon",
                author = "Test",
                version = "1.0.0",
                distributionProfile = AddonDistributionPolicy.DevelopmentProfile,
                buildId = Guid.NewGuid().ToString("N"),
                createdUtc = DateTime.UtcNow.ToString("o"),
                sdkVersion = "test",
                unityVersion = Application.unityVersion,
                contents = new[]
                {
                    new AddonManifest.ContentEntry
                    {
                        kind = AddonManifest.ModContentKind,
                        id = "community.hello",
                        mod = new AddonManifest.ModContent
                        {
                            manifest = "mods/community.hello/mod.json"
                        }
                    },
                    new AddonManifest.ContentEntry
                    {
                        kind = AddonManifest.MapContentKind,
                        id = "dm_replaced"
                    },
                    new AddonManifest.ContentEntry
                    {
                        kind = AddonManifest.MapContentKind,
                        id = "dm_preserved"
                    }
                }
            });

            Assert.That(AddonMapBuildService.TrySeedExistingContentExceptMap(
                sourceRoot, destinationRoot, "dm_replaced", out var error), Is.True, error);
            Assert.That(File.Exists(Path.Combine(destinationRoot, Constants.AddonManifestFileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(destinationRoot, "mods", "community.hello", "HelloMod.csx")),
                Is.True);
            Assert.That(Directory.Exists(Path.Combine(destinationRoot, Constants.MapsFolder, "dm_replaced")),
                Is.False);
            Assert.That(File.Exists(Path.Combine(destinationRoot, Constants.MapsFolder,
                "dm_preserved", "current.bundle")), Is.True);

            var replacement = new AddonManifest.ContentEntry
            {
                kind = AddonManifest.MapContentKind,
                id = "dm_replaced",
                title = "Replacement"
            };
            Assert.That(AddonHelper.TryLoad(destinationRoot, out var existingManifest, out error), Is.True,
                error);
            var merged = AddonMapBuildService.MergeMapContent(existingManifest, replacement);
            Assert.That(merged, Has.Length.EqualTo(3));
            Assert.That(merged, Has.Exactly(1).Matches<AddonManifest.ContentEntry>(entry =>
                entry.kind == AddonManifest.ModContentKind &&
                entry.id == AddonIdPolicy.NormalizeOrEmpty("community.hello")));
            Assert.That(merged, Has.Exactly(1).Matches<AddonManifest.ContentEntry>(entry =>
                entry.kind == AddonManifest.MapContentKind && entry.id == "dm_preserved"));
            Assert.That(merged, Has.Exactly(1).SameAs(replacement));

            var receiptPath = Path.Combine(destinationRoot, Constants.MapsFolder,
                "dm_preserved", Constants.CompilationReceiptFileName);
            var receipt = new MapCompilationReceipt
            {
                mapId = "dm_preserved",
                sceneAddress = "dm_preserved",
                contentKind = MapCompilationReceipt.AddonContentKind,
                distributionProfile = AddonDistributionPolicy.DevelopmentProfile,
                distributionPolicy = string.Empty,
                buildId = "old-build",
                compilerVersion = "old-sdk",
                unityVersion = "old-unity"
            };
            MapCompilationReceiptUtility.Seal(receipt);
            File.WriteAllText(receiptPath, MapCompilationReceiptUtility.Serialize(receipt));
            var rebuiltManifest = new AddonManifest
            {
                buildId = "new-build",
                distributionProfile = AddonDistributionPolicy.DevelopmentProfile,
                distributionPolicy = string.Empty,
                sdkVersion = "new-sdk",
                unityVersion = "new-unity",
                contents = new[]
                {
                    new AddonManifest.ContentEntry
                    {
                        kind = AddonManifest.MapContentKind,
                        id = "dm_preserved",
                        map = new AddonManifest.MapContent
                        {
                            scene = "dm_preserved",
                            compiledReceipt =
                                $"{Constants.MapsFolder}/dm_preserved/{Constants.CompilationReceiptFileName}"
                        }
                    }
                }
            };
            Assert.That(AddonMapBuildService.TryRefreshMapReceiptMetadata(
                destinationRoot, rebuiltManifest, out error), Is.True, error);
            Assert.That(MapCompilationReceiptUtility.TryLoad(receiptPath, out var refreshedReceipt), Is.True);
            Assert.That(refreshedReceipt.buildId, Is.EqualTo("new-build"));
            Assert.That(refreshedReceipt.compilerVersion, Is.EqualTo("new-sdk"));
            Assert.That(refreshedReceipt.unityVersion, Is.EqualTo("new-unity"));
            Assert.That(MapCompilationReceiptUtility.HasValidSeal(refreshedReceipt), Is.True);
        }

    }
}
