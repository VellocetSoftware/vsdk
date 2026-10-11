// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Editor.Audio;
using Vellocet.Sdk.Editor.Maps;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests
{
    public sealed class AddonAudioDependencyTests
    {
        [Test]
        public void CatalogPolicy_RequiresCurrentSchemaAndValidGain()
        {
            Assert.That(AddonAudioPolicy.TryResolveGain(3, 0.35f, out var gain), Is.True);
            Assert.That(gain, Is.EqualTo(0.35f));
            Assert.That(AddonAudioPolicy.TryResolveGain(3, -0.01f, out _), Is.False);
            Assert.That(AddonAudioPolicy.TryResolveGain(3, float.NaN, out _), Is.False);
            Assert.That(AddonAudioPolicy.IsSupportedSchemaVersion(2), Is.False);
            Assert.That(AddonAudioPolicy.IsSupportedSchemaVersion(4), Is.False);
        }

        [Test]
        public void Ambience2DTemplate_IsLoopingAndNonSpatial()
        {
            Assert.That(AddonAudioPolicy.TryParseTemplate(
                AddonAudioPolicy.Ambience2DLoopTemplate, out var template), Is.True);
            Assert.That(template, Is.EqualTo(AddonAudioTemplate.Ambience2DLoop));
            Assert.That(AddonAudioPolicy.IsLoop(template), Is.True);
            Assert.That(AddonAudioPolicy.IsSpatial(template), Is.False);
            Assert.That(AddonAudioPolicy.IsTemplateSupported(2, template), Is.False);
            Assert.That(AddonAudioPolicy.IsTemplateSupported(3, template), Is.True);
            Assert.That(AddonAudioPolicy.ToCatalogValue(template),
                Is.EqualTo(AddonAudioPolicy.Ambience2DLoopTemplate));
        }

        [Test]
        public void DependencyCue_IsARequirementAndNeverEmbeddedMedia()
        {
            AddonAudioCue cue = null;
            try
            {
                cue = ScriptableObject.CreateInstance<AddonAudioCue>();
                var cueObject = new SerializedObject(cue);
                cueObject.FindProperty("origin").enumValueIndex = (int)AddonAudioCueOrigin.AddonDependency;
                cueObject.FindProperty("cueId").stringValue = "community.audio_pack.round_start";
                cueObject.FindProperty("dependencyAddonId").stringValue = "community.audio_pack";
                cueObject.FindProperty("sourceAssetGuid").stringValue = "unused-source-guid";
                cueObject.ApplyModifiedPropertiesWithoutUndo();

                var succeeded = AddonAudioBuildPipeline.TryResolveMapCues(
                    "community.dependency_map",
                    new[] { cue },
                    out var embedded,
                    out var dependencies,
                    out var error);

                Assert.That(succeeded, Is.True, error);
                Assert.That(embedded, Is.Empty,
                    "Dependency cues must never enter the source-media copy pipeline.");
                Assert.That(dependencies, Is.EqualTo(new[] { "community.audio_pack" }));
            }
            finally
            {
                if (cue)
                    Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void Requirements_AreNormalizedDeduplicatedAndNeverSelfReference()
        {
            Assert.That(AddonMapBuildService.MergeRequirements(
                    "community.map",
                    new[] { " community.audio_pack ", "community.map", "BAD ID" },
                    new[] { "COMMUNITY.AUDIO_PACK", "community.weather" }),
                Is.EqualTo(new[] { "community.audio_pack", "community.weather" }));
        }

        [Test]
        public void DependencyOnlyExport_RemovesPreviouslyEmbeddedMapMedia()
        {
            var root = Path.Combine(Path.GetTempPath(), "vellocet-audio-cleanup-" + Guid.NewGuid().ToString("N"));
            var generated = Path.Combine(root, "audio", "community_map_audio");
            try
            {
                Directory.CreateDirectory(Path.Combine(generated, "sounds"));
                File.WriteAllBytes(Path.Combine(generated, "sounds", "old.mp3"), new byte[] { 1, 2, 3 });

                Assert.That(AddonAudioBuildPipeline.TryRemoveGeneratedMapAudio(
                    root, "community_map", out var error), Is.True, error);
                Assert.That(Directory.Exists(generated), Is.False,
                    "A dependency-only rebuild must not leave the provider's old media duplicated in the map package.");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}