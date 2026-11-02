// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_INCLUDE_TESTS && !ENABLE_JSON_CATALOG
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.TestTools;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class AddonCatalogCompatibilityTests
    {
        private const string MissingAssembly =
            "VellocetSdk.Core, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";

        [Test]
        public void CurrentAddressablesWriter_ProducesACompatibleCatalogWithoutLoadingAssets()
        {
            Assert.That(AddonCatalogCompatibility.TryValidate(CreateCatalog(), out var error), Is.True, error);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase("instance-provider")]
        [TestCase("scene-provider")]
        [TestCase("resource-provider")]
        public void ExistingButForbiddenProviderInitializer_IsRejected(string record)
        {
            var bytes = CreateCatalog();
            var type = FindTypeRecord(bytes, record);
            ReplaceStringReference(ref bytes, type, typeof(MapDefinition).Assembly.GetName().Name);
            ReplaceStringReference(ref bytes, type + 4, typeof(MapDefinition).FullName);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out var error), Is.False);
            Assert.That(error, Does.Contain("forbidden provider"));
        }

#if VELLOCET_ADDRESSABLES_4
        [Test]
        public void CoreLibraryKey_DoesNotEmbedAnEditorSpecificAssemblyName()
        {
            var bytes = CreateCatalog();
            Assert.That(ReadUInt(bytes, FindTypeRecord(bytes, "key")), Is.EqualTo(uint.MaxValue));
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out var error), Is.True, error);
        }

#endif

        [TestCase(0u)]
        [TestCase(4u)]
        [TestCase(8u)]
        public void InvalidLocationStrings_AreRejectedBeforeNativeReads(uint field)
        {
            var bytes = CreateCatalog();
            var location = ReadUInt(bytes, ReadUInt(bytes, ReadUInt(bytes, 8) + 4));
            WriteUInt(bytes, location + field, uint.MaxValue - 1);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out _), Is.False);
        }

        [Test]
        public void InvalidBundleOptionsPayload_IsRejectedBeforeNativeReads()
        {
            var bytes = CreateCatalog();
            var location = ReadUInt(bytes, ReadUInt(bytes, ReadUInt(bytes, 8) + 4));
            var extra = ReadUInt(bytes, location + 20);
            WriteUInt(bytes, extra + 4, uint.MaxValue - 1);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out _), Is.False);
        }

        [TestCase("resource")]
        [TestCase("key")]
        [TestCase("extra-data")]
        [TestCase("instance-provider")]
        [TestCase("scene-provider")]
        [TestCase("resource-provider")]
        public void MissingAssembly_IsRejectedQuietlyInEveryTypeBearingRecord(string record)
        {
            var bytes = CreateCatalog();
            var type = FindTypeRecord(bytes, record);
            ReplaceStringReference(ref bytes, type, MissingAssembly);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out var error), Is.False);
            Assert.That(error, Does.Contain("VellocetSdk.Core").And.Contain("Recompile this map"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingClassInAnExistingAssembly_AlsoRequiresRecompilation()
        {
            var bytes = CreateCatalog();
            var type = FindTypeRecord(bytes, "resource");
            ReplaceStringReference(ref bytes, type + 4, "Vellocet.Legacy.RemovedMapType");
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out var error), Is.False);
            Assert.That(error, Does.Contain("RemovedMapType").And.Contain("Recompile this map"));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(31)]
        public void TruncatedHeader_IsRejectedWithoutNativeDeserialization(int length)
        {
            var bytes = CreateCatalog();
            Array.Resize(ref bytes, length);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out _), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TruncatedLocationArray_IsRejected()
        {
            var bytes = CreateCatalog();
            var keys = ReadUInt(bytes, 8);
            var locations = ReadUInt(bytes, keys + 4);
            Array.Resize(ref bytes, (int)locations + 3);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out _), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(0u)]
        [TestCase(1u)]
#if VELLOCET_ADDRESSABLES_4
        [TestCase(2u)]
#else
        [TestCase(3u)]
#endif
        [TestCase(4u)]
        [TestCase(uint.MaxValue)]
        public void UnsupportedCatalogVersion_FailsClosed(uint version)
        {
            var bytes = CreateCatalog();
            WriteUInt(bytes, 4, version);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out var error), Is.False);
            Assert.That(error, Does.Contain("header or version"));
        }

        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(uint.MaxValue - 1)]
        [TestCase(uint.MaxValue)]
        public void InvalidKeyOffsets_CannotReadOutsideTheManagedBuffer(uint offset)
        {
            var bytes = CreateCatalog();
            WriteUInt(bytes, 8, offset);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out _), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(7u)]
        [TestCase(uint.MaxValue)]
        public void MalformedArraySizes_AreRejected(uint size)
        {
            var bytes = CreateCatalog();
            WriteUInt(bytes, ReadUInt(bytes, 8) - 4, size);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out _), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(4097)]
        [TestCase(13375)]
        [TestCase(AddonCatalogCompatibility.MaximumKeys)]
        public void LargeValidKeyTables_AreAccepted(int count)
        {
            var bytes = CreateCatalog(keyCount: count);
            Assert.That(ReadUInt(bytes, ReadUInt(bytes, 8) - 4) / 8, Is.EqualTo(count));
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out var error), Is.True, error);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TooManyKeys_ReportsTheTableCountAndLimit()
        {
            var count = AddonCatalogCompatibility.MaximumKeys + 1;
            Assert.That(AddonCatalogCompatibility.TryValidate(CreateCatalog(keyCount: count), out var error), Is.False);
            Assert.That(error, Does.Contain("key table").And.Contain(count.ToString())
                .And.Contain(AddonCatalogCompatibility.MaximumKeys.ToString()));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(AddonCatalogCompatibility.MaximumLocations, true)]
        [TestCase(AddonCatalogCompatibility.MaximumLocations + 1, false)]
        public void LocationBudget_RemainsIndependentOfKeyBudget(int count, bool accepted)
        {
            Assert.That(AddonCatalogCompatibility.TryValidate(CreateCatalog(locationCount: count), out var error),
                Is.EqualTo(accepted), error);
            if (!accepted)
                Assert.That(error, Does.Contain(count.ToString()).And.Contain("locations")
                    .And.Contain(AddonCatalogCompatibility.MaximumLocations.ToString()));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(AddonCatalogCompatibility.MaximumProviderInitializers, true)]
        [TestCase(AddonCatalogCompatibility.MaximumProviderInitializers + 1, false)]
        public void ProviderBudget_DoesNotGrowWithKeyBudget(int count, bool accepted)
        {
            Assert.That(AddonCatalogCompatibility.TryValidate(CreateCatalog(providerCount: count), out var error),
                Is.EqualTo(accepted), error);
            if (!accepted)
                Assert.That(error, Does.Contain("provider table").And.Contain(count.ToString())
                    .And.Contain(AddonCatalogCompatibility.MaximumProviderInitializers.ToString()));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CyclicDynamicTypeString_IsBoundedAndRejected()
        {
            var bytes = CreateCatalog();
            var type = FindTypeRecord(bytes, "resource");
            ReplaceStringReference(ref bytes, type, "Cycle");
            var part = ReadUInt(bytes, type);
            var node = (uint)bytes.Length;
            Array.Resize(ref bytes, bytes.Length + 8);
            WriteUInt(bytes, node, part);
            WriteUInt(bytes, node + 4, node);
            WriteUInt(bytes, type, 0x40000000 | node);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out var error), Is.False);
            Assert.That(error, Does.Contain("cyclic").Or.Contain("string limit"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void UnicodeTypeNames_AreDecodedWithoutFallingBackToTheBinaryDeserializer()
        {
            var bytes = CreateCatalog();
            var type = FindTypeRecord(bytes, "resource");
            ReplaceStringReference(ref bytes, type + 4, "Vellocet.Missing.Épreuve", true);
            Assert.That(AddonCatalogCompatibility.TryValidate(bytes, out var error), Is.False);
            Assert.That(error, Does.Contain("Épreuve").And.Contain("Recompile this map"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OversizedCatalog_IsRejectedBeforeParsing()
        {
            Assert.That(AddonCatalogCompatibility.TryValidate(
                new byte[AddonCatalogCompatibility.MaximumCatalogBytes + 1], out var error), Is.False);
            Assert.That(error, Does.Contain("size limit"));
        }

        [Test]
        public void FilePreflight_IsReadOnlyAndReportsMissingFilesWithoutLoggingAnException()
        {
            var directory = Path.Combine(Path.GetTempPath(), "aniv-catalog-preflight-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "catalog.bin");
                Assert.That(AddonCatalogCompatibility.TryValidateFile(path, out var missing), Is.False);
                Assert.That(missing, Is.Not.Empty);
                var bytes = CreateCatalog();
                File.WriteAllBytes(path, bytes);
                Assert.That(AddonCatalogCompatibility.TryValidateFile(path, out var error), Is.True, error);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        internal static byte[] CreateCatalog(int keyCount = 3, int locationCount = 3, int providerCount = 2)
        {
            var entries = new List<ContentCatalogDataEntry>
            {
                new(typeof(IAssetBundleResource), "Addons/maps/arena/StandaloneOSX/map.bundle",
                    typeof(AssetBundleProvider).FullName, new object[] { "bundle" }, null,
                    new AssetBundleRequestOptions { BundleName = "map", Hash = "12345678123456781234567812345678" }),
                new(typeof(SceneInstance), "Assets/Maps/Arena.unity", typeof(BundledAssetProvider).FullName,
                    new object[] { "arena" }, new object[] { "bundle" }),
                new(typeof(MapDefinition), "Assets/Maps/Arena.asset", typeof(BundledAssetProvider).FullName,
                    new object[] { "definition" }, new object[] { "bundle" })
            };
            for (var i = entries.Count; i < locationCount; i++)
                entries.Add(new ContentCatalogDataEntry(typeof(MapDefinition), $"Assets/Maps/Extra{i}.asset",
                    typeof(BundledAssetProvider).FullName, new object[] { "asset-" + i }, new object[] { "bundle" }));
            for (var i = entries.Count; i < keyCount; i++)
                entries[1].Keys.Add("alias-" + i);
#if VELLOCET_ADDRESSABLES_4
            var data = new BinaryContentCatalogData("preflight-test")
#else
            var data = new ContentCatalogData(entries, "preflight-test")
#endif
            {
                InstanceProviderData = ObjectInitializationData.CreateSerializedInitializationData<InstanceProvider>(),
                SceneProviderData = ObjectInitializationData.CreateSerializedInitializationData(
                    typeof(ISceneProvider).Assembly.GetType(
                        "UnityEngine.ResourceManagement.ResourceProviders.SceneProvider"))
            };
            data.ResourceProviderData.Add(ObjectInitializationData
                .CreateSerializedInitializationData<AssetBundleProvider>());
            data.ResourceProviderData.Add(ObjectInitializationData
                .CreateSerializedInitializationData<BundledAssetProvider>());
            for (var i = data.ResourceProviderData.Count; i < providerCount; i++)
                data.ResourceProviderData.Add(ObjectInitializationData
                    .CreateSerializedInitializationData<BundledAssetProvider>());
#if VELLOCET_ADDRESSABLES_4
            data.SetData(entries);
#endif
            return data.SerializeToByteArray();
        }

        // Mutate references in a real package-generated catalog; never invoke Unity's logging
        // deserializer on the broken fixture or install fake assembly resolvers to make it load.
        private static uint FindTypeRecord(byte[] bytes, string record)
        {
            var keys = ReadUInt(bytes, 8);
            var firstLocation = ReadUInt(bytes, ReadUInt(bytes, keys + 4));
            return record switch
            {
                "resource" => ReadUInt(bytes, firstLocation + 24),
                "key" => ReadUInt(bytes, ReadUInt(bytes, keys)),
                "extra-data" => ReadUInt(bytes, ReadUInt(bytes, firstLocation + 20)),
                "instance-provider" => ReadUInt(bytes, ReadUInt(bytes, 16) + 4),
                "scene-provider" => ReadUInt(bytes, ReadUInt(bytes, 20) + 4),
                "resource-provider" => ReadUInt(bytes, ReadUInt(bytes, ReadUInt(bytes, 24)) + 4),
                _ => throw new ArgumentOutOfRangeException(nameof(record))
            };
        }

        private static void ReplaceStringReference(ref byte[] bytes, uint reference, string value, bool unicode = false)
        {
            var encoded = (unicode ? Encoding.Unicode : Encoding.ASCII).GetBytes(value);
            var offset = (uint)bytes.Length;
            Array.Resize(ref bytes, bytes.Length + 4 + encoded.Length);
            WriteUInt(bytes, offset, (uint)encoded.Length);
            Array.Copy(encoded, 0, bytes, (int)offset + 4, encoded.Length);
            WriteUInt(bytes, reference, (offset + 4) | (unicode ? 0x80000000 : 0));
        }

        private static uint ReadUInt(byte[] bytes, uint offset)
        {
            return BitConverter.ToUInt32(bytes, (int)offset);
        }

        private static void WriteUInt(byte[] bytes, uint offset, uint value)
        {
            Array.Copy(BitConverter.GetBytes(value), 0, bytes, (int)offset, 4);
        }


    }
}
#endif
