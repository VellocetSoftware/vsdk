// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Editor.Addons;
using Vellocet.Sdk.Editor.Maps;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests
{
    public sealed class AddonContentAuthoringTests
    {
        [Test]
        public void AuthoringProjectIdentity_RejectsSelfDependency()
        {
            var project = ScriptableObject.CreateInstance<AddonAuthoringProject>();
            try
            {
                SetProject(project, "community.audio", "Community Audio", "1.0.0",
                    new[] { "community.audio" }, Array.Empty<Object>());

                Assert.That(AddonAuthoringProjectPolicy.TryValidateIdentity(project, out var error), Is.False);
                Assert.That(error, Does.Contain("cannot require itself"));
            }
            finally
            {
                Object.DestroyImmediate(project);
            }
        }

        [Test]
        public void ManifestBuildUtility_OwnsVolatileBuildFields()
        {
            var manifest = new AddonManifest();
            var buildId = AddonManifestBuildUtility.CreateBuildId();
            var timestamp = new DateTime(2026, 8, 26, 12, 30, 0, DateTimeKind.Utc);

            AddonManifestBuildUtility.ApplyGeneratedMetadata(
                manifest, AddonMapBuildProfile.Release, buildId, "9.8.7", timestamp);

            Assert.That(manifest.schemaVersion, Is.EqualTo(AddonManifest.CurrentSchemaVersion));
            Assert.That(manifest.distributionProfile, Is.EqualTo(AddonDistributionPolicy.ReleaseProfile));
            Assert.That(manifest.distributionPolicy, Is.EqualTo(AddonDistributionPolicy.ReleasePolicyV1));
            Assert.That(manifest.buildId, Is.EqualTo(buildId));
            Assert.That(Guid.TryParseExact(manifest.buildId, "N", out _), Is.True);
            Assert.That(DateTimeOffset.TryParse(manifest.createdUtc, out var parsed), Is.True);
            Assert.That(parsed.UtcDateTime, Is.EqualTo(timestamp));
            Assert.That(manifest.sdkVersion, Is.EqualTo("9.8.7"));
            Assert.That(manifest.unityVersion, Is.EqualTo(Application.unityVersion));
        }

        [Test]
        public void ThumbnailBuildUtility_RequiresReleaseArtwork()
        {
            Assert.That(AddonThumbnailBuildUtility.TryValidateFile(
                string.Empty, false, false, out var developmentError), Is.True);
            Assert.That(developmentError, Is.Null);

            Assert.That(AddonThumbnailBuildUtility.TryValidateFile(
                string.Empty, true, true, out var releaseError), Is.False);
            Assert.That(releaseError, Does.Contain("1920 × 1080"));

            var path = Path.Combine(Path.GetTempPath(), "vellocet-thumbnail-" + Guid.NewGuid().ToString("N") +
                                                        ".png");
            var texture = new Texture2D(64, 32, TextureFormat.RGB24, false);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Assert.That(AddonThumbnailBuildUtility.TryValidateFile(
                    path, false, false, out developmentError), Is.True, developmentError);
                Assert.That(AddonThumbnailBuildUtility.TryValidateFile(
                    path, true, true, out releaseError), Is.False);
                Assert.That(releaseError, Does.Contain("64 × 32"));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ExporterRegistry_ResolvesCollectionsButNotIndividualCues()
        {
            var collection = ScriptableObject.CreateInstance<AddonAudioCollection>();
            var cue = ScriptableObject.CreateInstance<AddonAudioCue>();
            try
            {
                AddonContentSourceExporterRegistry.Invalidate();
                Assert.That(AddonContentSourceExporterRegistry.TryResolve(collection, out var exporter,
                    out var error), Is.True, error);
                Assert.That(exporter.ContentKind, Is.EqualTo(AddonManifest.AudioContentKind));
                Assert.That(AddonContentSourceExporterRegistry.TryResolve(cue, out _, out error), Is.False);
                Assert.That(error, Does.Contain("No add-on content exporter"));
            }
            finally
            {
                Object.DestroyImmediate(collection);
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void ContentBuild_CreatesValidatedAudioPackage()
        {
            var token = Guid.NewGuid().ToString("N");
            var assetFolder = "Assets/__SdkContentBuildTests_" + token;
            var audioPath = assetFolder + "/tone.wav";
            var exportRoot = Path.Combine(Path.GetTempPath(), "vellocet-content-build-" + token);
            AddonAuthoringProject project = null;
            AddonAudioCollection collection = null;
            AddonAudioCue cue = null;
            try
            {
                Directory.CreateDirectory(assetFolder);
                WriteMinimalWav(audioPath);
                AssetDatabase.ImportAsset(audioPath, ImportAssetOptions.ForceSynchronousImport);
                var audioGuid = AssetDatabase.AssetPathToGUID(audioPath);
                Assert.That(audioGuid, Is.Not.Empty);

                cue = ScriptableObject.CreateInstance<AddonAudioCue>();
                var cueSerialized = new SerializedObject(cue);
                cueSerialized.FindProperty("cueId").stringValue = "tests.content_audio.tone";
                cueSerialized.FindProperty("sourceAssetGuid").stringValue = audioGuid;
                cueSerialized.ApplyModifiedPropertiesWithoutUndo();

                collection = ScriptableObject.CreateInstance<AddonAudioCollection>();
                var collectionSerialized = new SerializedObject(collection);
                collectionSerialized.FindProperty("addonId").stringValue = "tests.content_audio";
                collectionSerialized.FindProperty("contentId").stringValue = "tests_content_audio";
                collectionSerialized.FindProperty("title").stringValue = "Test Audio";
                var cues = collectionSerialized.FindProperty("cues");
                cues.arraySize = 1;
                cues.GetArrayElementAtIndex(0).objectReferenceValue = cue;
                collectionSerialized.ApplyModifiedPropertiesWithoutUndo();

                project = ScriptableObject.CreateInstance<AddonAuthoringProject>();
                SetProject(project, "tests.content_audio", "Content Audio", "1.0.0",
                    Array.Empty<string>(), new Object[] { collection });
                Directory.CreateDirectory(exportRoot);

                var result = AddonContentBuildService.Build(new AddonContentBuildRequest
                {
                    Project = project,
                    PackageOutputRoot = exportRoot,
                    Profile = AddonMapBuildProfile.Development
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(File.Exists(Path.Combine(result.AddonRoot, Constants.AddonManifestFileName)), Is.True);
                Assert.That(result.Manifest.contents, Has.Exactly(1).Matches<AddonManifest.ContentEntry>(value =>
                    value.kind == AddonManifest.AudioContentKind && value.id == "tests_content_audio"));
                Assert.That(AddonPackageValidator.TryValidate(result.AddonRoot,
                        AddonPackageValidationMode.Standard, out _, out var validationError), Is.True,
                    validationError);
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(assetFolder))
                    AssetDatabase.DeleteAsset(assetFolder);
                if (Directory.Exists(exportRoot))
                    Directory.Delete(exportRoot, true);
                if (project)
                    Object.DestroyImmediate(project);
                if (collection)
                    Object.DestroyImmediate(collection);
                if (cue)
                    Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void AudioPackCreation_BuildsLinkedAssetsAndBulkImportsSelectedMedia()
        {
            var token = Guid.NewGuid().ToString("N");
            var assetFolder = "Assets/__SdkAudioPackCreationTests_" + token;
            try
            {
                var created = AddonAuthoringAssetUtility.CreateAudioPack(new AddonAudioPackCreationRequest
                {
                    AssetFolder = assetFolder,
                    AddonId = "tests.audio_pack",
                    Title = "Audio Pack",
                    Author = "Test Author",
                    Version = "1.2.3"
                });

                Assert.That(created.Success, Is.True, created.Error);
                Assert.That(created.Project.AddonId, Is.EqualTo("tests.audio_pack"));
                Assert.That(created.Project.Title, Is.EqualTo("Audio Pack"));
                Assert.That(created.Project.Author, Is.EqualTo("Test Author"));
                Assert.That(created.Project.Version, Is.EqualTo("1.2.3"));
                Assert.That(created.Project.ContentSources, Is.EqualTo(new Object[]
                {
                    created.Collection
                }));
                Assert.That(created.Collection.AddonId, Is.EqualTo("tests.audio_pack"));

                var audioPath = created.AssetRoot + "/Audio/round-start.wav";
                WriteMinimalWav(audioPath);
                AssetDatabase.ImportAsset(audioPath, ImportAssetOptions.ForceSynchronousImport);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
                Assert.That(clip, Is.Not.Null);

                var imported = AddonAuthoringAssetUtility.CreateAudioCuesFromSelection(
                    created.Project, created.Collection, new Object[] { clip });
                Assert.That(imported.Success, Is.True, imported.Error);
                Assert.That(imported.Cues, Has.Length.EqualTo(1));
                Assert.That(imported.Cues[0].CueId, Is.EqualTo("tests.audio_pack.round-start"));
                Assert.That(imported.Cues[0].SourceAssetGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(audioPath)));
                Assert.That(created.Collection.Cues, Does.Contain(imported.Cues[0]));

                var duplicate = AddonAuthoringAssetUtility.CreateAudioCuesFromSelection(
                    created.Project, created.Collection, new Object[] { clip });
                Assert.That(duplicate.Success, Is.True, duplicate.Error);
                Assert.That(duplicate.Cues, Is.Empty);
                Assert.That(duplicate.SkippedCount, Is.EqualTo(1));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(assetFolder))
                    AssetDatabase.DeleteAsset(assetFolder);
            }
        }

        private static void SetProject(AddonAuthoringProject project, string addonId, string title,
            string version, string[] requirements, Object[] sources)
        {
            var serialized = new SerializedObject(project);
            serialized.FindProperty("addonId").stringValue = addonId;
            serialized.FindProperty("title").stringValue = title;
            serialized.FindProperty("version").stringValue = version;
            var required = serialized.FindProperty("requiredAddonIds");
            required.arraySize = requirements.Length;
            for (var index = 0; index < requirements.Length; index++)
                required.GetArrayElementAtIndex(index).stringValue = requirements[index];
            var contentSources = serialized.FindProperty("contentSources");
            contentSources.arraySize = sources.Length;
            for (var index = 0; index < sources.Length; index++)
                contentSources.GetArrayElementAtIndex(index).objectReferenceValue = sources[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteMinimalWav(string path)
        {
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(38);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(8000);
            writer.Write(16000);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(2);
            writer.Write((short)0);
        }
    }
}