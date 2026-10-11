// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Vellocet.Sdk.Editor.ContentLinking;
using Vellocet.Sdk.Editor.Tools;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Tests
{
    public sealed class SdkContentLayoutTests
    {
        [Test]
        public void EveryContentRoot_RoundTripsBetweenSourceAndInstalledLayouts()
        {
            foreach (var mapping in Constants.SdkAssetRootMappings)
            {
                var source = mapping.SourceRoot + "/Nested/Content.asset";
                var installed = mapping.InstalledRoot + "/Nested/Content.asset";
                Assert.That(SdkContentPathUtility.IsSourceSdkAssetPath(source), Is.True);
                Assert.That(SdkContentPathUtility.IsInstalledSdkAssetPath(installed), Is.True);
                Assert.That(SdkContentPathUtility.TryMapSourceToInstalledPath(source, out var mapped), Is.True);
                Assert.That(mapped, Is.EqualTo(installed));
                Assert.That(SdkContentPathUtility.TryMapInstalledToSourcePath(installed, out mapped), Is.True);
                Assert.That(mapped, Is.EqualTo(source));
            }
        }

        [Test]
        public void SharedAssets_MapIntoReadOnlySdkRoot_WithStableAddresses()
        {
            Assert.That(
                SdkContentPathUtility.TryMapSourceToInstalledPath(
                    "Assets/Art/Materials/Environment/Stone.mat",
                    out var installedPath
                ),
                Is.True
            );
            Assert.That(
                installedPath,
                Is.EqualTo("Assets/VellocetSDK/Materials/Environment/Stone.mat")
            );
            Assert.That(
                SdkContentPathUtility.TryMapInstalledToSourcePath(
                    "Assets/VellocetSDK/Materials/Environment/Stone.mat",
                    out var sourcePath
                ),
                Is.True
            );
            Assert.That(sourcePath, Is.EqualTo("Assets/Art/Materials/Environment/Stone.mat"));
            Assert.That(
                SdkContentPathUtility.TryMapSourceToInstalledPath(
                    "Assets/Art/Materials/Environment/Stone.mat.meta",
                    out var installedPath2
                ),
                Is.True
            );
            Assert.That(
                installedPath2,
                Is.EqualTo("Assets/VellocetSDK/Materials/Environment/Stone.mat.meta")
            );
            Assert.That(
                SharedAssetAddressUtility.CreateAddress(
                    "Assets/Art/Materials/Environment/Stone.mat"
                ),
                Is.EqualTo("SDK/Art/Materials/Environment/Stone.mat")
            );
            Assert.That(
                SharedAssetAddressUtility.CreateAddress(
                    "Assets/VellocetSDK/Materials/Environment/Stone.mat"
                ),
                Is.EqualTo("SDK/Art/Materials/Environment/Stone.mat")
            );
        }

        [Test]
        public void OrganizedProps_MapIntoMatchingSdkSubfolders()
        {
            Assert.That(
                SdkContentPathUtility.TryMapSourceToInstalledPath(
                    "Assets/Art/Models/Props/Prefabs/Furniture/furniture_table.prefab",
                    out var installedPath
                ),
                Is.True
            );
            Assert.That(
                installedPath,
                Is.EqualTo(
                    "Assets/VellocetSDK/Models/Props/Prefabs/Furniture/furniture_table.prefab"
                )
            );
            Assert.That(
                SdkContentPathUtility.TryMapInstalledToSourcePath(
                    "Assets/VellocetSDK/Models/Props/Prefabs/Furniture/furniture_table.prefab",
                    out var sourcePath
                ),
                Is.True
            );
            Assert.That(
                sourcePath,
                Is.EqualTo("Assets/Art/Models/Props/Prefabs/Furniture/furniture_table.prefab")
            );
        }

        [Test]
        public void ContentSchema_AcceptsOnlyTheCurrentVersion()
        {
            Assert.That(
                Constants.IsCurrentSdkContentSchemaVersion(Constants.SdkContentSchemaVersion),
                Is.True);
            Assert.That(
                Constants.IsCurrentSdkContentSchemaVersion(Constants.SdkContentSchemaVersion - 1),
                Is.False);
            Assert.That(
                Constants.IsCurrentSdkContentSchemaVersion(Constants.SdkContentSchemaVersion + 1),
                Is.False);
        }

        [Test]
        public void UnityMetaGuidParser_NormalizesValidGuidAndRejectsMalformedValues()
        {
            Assert.That(
                SdkUnityMetaFileUtility.TryParseGuid(
                    new string[2]
                    {
                        "fileFormatVersion: 2",
                        "guid: B4BC7D1AACB8D4778BE9AECDFA23BCC2"
                    },
                    out var guid
                ),
                Is.True
            );
            Assert.That(guid, Is.EqualTo("b4bc7d1aacb8d4778be9aecdfa23bcc2"));
            Assert.That(
                SdkUnityMetaFileUtility.TryParseGuid(
                    new string[2] { "fileFormatVersion: 2", "guid: not-a-guid" },
                    out _
                ),
                Is.False
            );
        }

        [Test]
        public void ManagedFileVerifier_AcceptsOnlyTheKnownUrpMaterialNormalization()
        {
            var bytes = Encoding.UTF8.GetBytes(
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  m_Name: Test\n"
            );
            var entry = CreateEntry("Assets/VellocetSDK/Materials/Props/test.mat", bytes);
            var bytes2 = Encoding.UTF8.GetBytes(
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &-7936817940303615300\nMonoBehaviour:\n  m_ObjectHideFlags: 11\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion\n  version: 10\n--- !u!21 &2100000\nMaterial:\n  m_Name: Test\n"
            );
            var bytes3 = Encoding.UTF8.GetBytes(
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  m_Name: Test\n--- !u!114 &-7936817940303615300\nMonoBehaviour:\n  m_ObjectHideFlags: 11\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion\n  version: 10\n"
            );
            Assert.That(SdkManagedFileVerifier.DoesBytesMatch(bytes2, entry), Is.True);
            Assert.That(SdkManagedFileVerifier.DoesBytesMatch(bytes3, entry), Is.True);
            var bytes4 = Encoding.UTF8.GetBytes(
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &-7936817940303615300\nMonoBehaviour:\n  m_ObjectHideFlags: 11\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion\n  version: 10\n--- !u!21 &2100000\nMaterial:\n  m_Name: Test\n"
                    .Replace(
                        "m_Name: Test",
                        "m_Name: Customized"
                    )
            );
            Assert.That(SdkManagedFileVerifier.DoesBytesMatch(bytes4, entry), Is.False);
            var bytes5 = Encoding.UTF8.GetBytes(
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &-7936817940303615300\nMonoBehaviour:\n  m_ObjectHideFlags: 11\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion\n  version: 10\n--- !u!21 &2100000\nMaterial:\n  m_Name: Test\n"
                    .Replace(
                        "  version: 10\n",
                        "  unexpected: 1\n  version: 10\n"
                    )
            );
            Assert.That(SdkManagedFileVerifier.DoesBytesMatch(bytes5, entry), Is.False);
        }

        [Test]
        public void VerificationCache_BindsFastReceiptToManifestHashAndFileStamp()
        {
            var entry = new SdkContentManifestEntry
            {
                relativePath = "Assets/VellocetSDK/Models/Props/test.prefab",
                bytes = 128,
                sha256 = new string('a', 64),
                sourceLastWriteUtcTicks = 123456789
            };
            var record = new SdkContentOwnershipStore.VerificationRecord
            {
                relativePath = entry.relativePath,
                manifestSha256 = entry.sha256,
                fileBytes = entry.bytes,
                lastWriteUtcTicks = 987654321
            };

            Assert.That(
                SdkContentVerificationCache.DoesVerificationRecordMatch(
                    record,
                    entry,
                    128,
                    987654321
                ),
                Is.True
            );

            var differentRevision = new SdkContentManifestEntry
            {
                relativePath = entry.relativePath,
                bytes = entry.bytes,
                sha256 = new string('b', 64),
                sourceLastWriteUtcTicks = entry.sourceLastWriteUtcTicks
            };
            Assert.That(
                SdkContentVerificationCache.DoesVerificationRecordMatch(
                    record,
                    differentRevision,
                    128,
                    987654321
                ),
                Is.False
            );
            Assert.That(
                SdkContentVerificationCache.DoesVerificationRecordMatch(
                    record,
                    entry,
                    128,
                    987654322
                ),
                Is.False
            );
        }

        [Test]
        public void CustomContent_IsOutsideEverySdkOwnedRoot()
        {
            var projectRelativePath = "Assets/CustomContent/Materials/Stone.mat";
            Assert.That(SdkContentPathUtility.IsSdkAssetPath(projectRelativePath), Is.False);
            Assert.That(
                Constants.SdkAssetRootMappings.Select(value => value.SourceRoot),
                Is.Unique
            );
            Assert.That(
                Constants.SdkAssetRootMappings.Select(value => value.InstalledRoot),
                Is.Unique
            );
            Assert.That(
                Constants.SdkAssetRootMappings.All(value =>
                    value.InstalledRoot.StartsWith("Assets/VellocetSDK/", StringComparison.Ordinal)
                ),
                Is.True
            );
        }

        [Test]
        public void ExportPolicy_AllowsOnlyLiveAddonsRootWithinRuntimeData()
        {
            var text = SdkPathDefaults.NormalizePath(Application.persistentDataPath);
            var text2 = Path.Combine(text, "addons");
            Assert.That(SdkPackageOutputPathPolicy.IsReservedRuntimeDataRoot(text2), Is.False);
            Assert.That(SdkPackageOutputPathPolicy.IsReservedRuntimeDataRoot(text), Is.True);
            Assert.That(
                SdkPackageOutputPathPolicy.IsReservedRuntimeDataRoot(Path.Combine(text, "cache")),
                Is.True
            );
            Assert.That(
                SdkPackageOutputPathPolicy.IsReservedRuntimeDataRoot(Path.Combine(text2, "example")),
                Is.True
            );
        }

        [Test]
        public void ExportPolicy_AllowsOnlyLiveAddonsRootWithinMarkerConfirmedRuntimeData()
        {
            var text = Path.Combine(
                Path.GetTempPath(),
                "vellocet-runtime-data-" + Guid.NewGuid().ToString("N")
            );
            var text2 = Path.Combine(text, "addons");
            var text3 = Path.Combine(text2, "example");
            try
            {
                Directory.CreateDirectory(text3);
                Directory.CreateDirectory(Path.Combine(text, "cache"));
                File.WriteAllText(
                    Path.Combine(text, "data-layout.json"),
                    "{\"format\":\"aneurism-iv-data/v1\",\"role\":\"client\"}"
                );
                Assert.That(SdkPackageOutputPathPolicy.IsReservedRuntimeDataRoot(text2), Is.False);
                Assert.That(
                    SdkPackageOutputPathPolicy.TryResolve(text2, out var normalizedPath, out var error),
                    Is.True,
                    error
                );
                Assert.That(normalizedPath, Is.EqualTo(SdkPathDefaults.NormalizePath(text2)));
                Assert.That(SdkPackageOutputPathPolicy.IsReservedRuntimeDataRoot(text), Is.True);
                Assert.That(
                    SdkPackageOutputPathPolicy.IsReservedRuntimeDataRoot(Path.Combine(text, "cache")),
                    Is.True
                );
                Assert.That(SdkPackageOutputPathPolicy.IsReservedRuntimeDataRoot(text3), Is.True);
            }
            finally
            {
                if (Directory.Exists(text))
                    Directory.Delete(text, true);
            }
        }

        private static SdkContentManifestEntry CreateEntry(string relativePath, byte[] contents)
        {
            using var sHA = SHA256.Create();
            return new SdkContentManifestEntry
            {
                relativePath = relativePath,
                bytes = contents.LongLength,
                sha256 = BitConverter
                    .ToString(sHA.ComputeHash(contents))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant()
            };
        }

    }
}
