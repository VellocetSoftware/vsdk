using System;
using NUnit.Framework;
using Vellocet.Sdk.Maps;
using Mismatch = Vellocet.Sdk.Maps.MapCompilationReceiptValidator.IdentityMismatch;

namespace Vellocet.Sdk.Tests
{
    public sealed class MapCompilationReceiptValidatorTests
    {
        [TestCase("base", "sk_test", "scene", StringComparison.Ordinal, Mismatch.None)]
        [TestCase("BASE", "SK_TEST", "scene", StringComparison.OrdinalIgnoreCase, Mismatch.None)]
        [TestCase("BASE", "sk_test", "scene", StringComparison.Ordinal, Mismatch.ContentKind)]
        [TestCase("base", "SK_TEST", "scene", StringComparison.Ordinal, Mismatch.MapId)]
        [TestCase("addon", "sk_test", "scene", StringComparison.OrdinalIgnoreCase, Mismatch.ContentKind)]
        [TestCase("base", "other", "scene", StringComparison.OrdinalIgnoreCase, Mismatch.MapId)]
        [TestCase("base", "sk_test", "Scene", StringComparison.OrdinalIgnoreCase, Mismatch.SceneAddress)]
        public void IdentityChecks_PreserveCallerComparisonRules(string kind, string id, string address,
            StringComparison comparison, Mismatch expected)
        {
            var receipt = new MapCompilationReceipt { contentKind = kind, mapId = id, sceneAddress = address };
            Assert.That(MapCompilationReceiptValidator.GetIdentityMismatch(receipt, "base", "sk_test", "scene",
                comparison), Is.EqualTo(expected));
        }

        [Test]
        public void AuthoringIdentityCheck_LeavesSceneAddressToItsExistingLayoutChecks()
        {
            var receipt = new MapCompilationReceipt { contentKind = "base", mapId = "sk_test", sceneAddress = "other" };
            Assert.That(MapCompilationReceiptValidator.GetIdentityMismatch(receipt, "base", "sk_test", null,
                StringComparison.Ordinal), Is.EqualTo(Mismatch.None));
        }
    }
}
