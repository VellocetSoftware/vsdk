// Binary content catalogs are an Addressables 4 format.
#if VELLOCET_ADDRESSABLES_4
// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_INCLUDE_TESTS && !ENABLE_JSON_CATALOG
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Editor.Maps;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class AddonCatalogExportTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void InvalidCatalog_IsRejectedBeforeReplacingPreviousOutput(bool exceedsKeyLimit)
        {
            var root = Path.Combine(Path.GetTempPath(), "aniv-catalog-export-" + Guid.NewGuid().ToString("N"));
            try
            {
                var scratch = Path.Combine(root, "scratch");
                var output = Path.Combine(root, "output");
                Directory.CreateDirectory(scratch);
                Directory.CreateDirectory(output);
                var previous = Path.Combine(output, "previous.bundle");
                File.WriteAllText(previous, "previous verified package");
                var bytes = AddonCatalogCompatibilityTests.CreateCatalog(
                    keyCount: exceedsKeyLimit ? AddonCatalogCompatibility.MaximumKeys + 1 : 3);
                if (!exceedsKeyLimit)
                {
                    var keys = BitConverter.ToUInt32(bytes, 8);
                    Array.Copy(BitConverter.GetBytes(7u), 0, bytes, (int)keys - 4, 4);
                }
                File.WriteAllBytes(Path.Combine(scratch, "catalog.bin"), bytes);
                File.WriteAllBytes(Path.Combine(scratch, "new.bundle"), new byte[] { 1, 2, 3 });

                Assert.That(Exporter.PackageFromScratch(scratch, output, out var catalog, out var bundles,
                    out var error), Is.False);
                Assert.That(error, Does.Contain("key table"));
                Assert.That(error, Does.Contain(exceedsKeyLimit ? "limit" : "malformed"));
                Assert.That(catalog, Is.Null);
                Assert.That(bundles, Is.Empty);
                Assert.That(Directory.GetFiles(output), Is.EquivalentTo(new[] { previous }));
                Assert.That(File.ReadAllText(previous), Is.EqualTo("previous verified package"));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void LargeCompatibleCatalog_IsCopiedAndRemainsRuntimeCompatible()
        {
            var root = Path.Combine(Path.GetTempPath(), "aniv-catalog-export-" + Guid.NewGuid().ToString("N"));
            try
            {
                var scratch = Path.Combine(root, "scratch");
                var output = Path.Combine(root, "output");
                Directory.CreateDirectory(scratch);
                var bytes = AddonCatalogCompatibilityTests.CreateCatalog(keyCount: 13375);
                File.WriteAllBytes(Path.Combine(scratch, "catalog.bin"), bytes);
                var bundle = new byte[] { 1, 2, 3 };
                File.WriteAllBytes(Path.Combine(scratch, "map.bundle"), bundle);

                LogAssert.Expect(UnityEngine.LogType.Log,
                    $"[Map Builder] Copied 1 bundle(s) + catalog to {output}");
                Assert.That(Exporter.PackageFromScratch(scratch, output, out var catalog, out var bundles,
                    out var error), Is.True, error);
                Assert.That(AddonCatalogCompatibility.TryValidateFile(catalog, out error), Is.True, error);
                Assert.That(File.ReadAllBytes(catalog), Is.EqualTo(bytes));
                Assert.That(bundles, Has.Length.EqualTo(1));
                Assert.That(File.ReadAllBytes(bundles[0]), Is.EqualTo(bundle));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
#endif
#endif
