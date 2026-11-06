using System;
using System.IO;
using NUnit.Framework;
using Vellocet.Sdk.Addons;

namespace Vellocet.Sdk.Tests
{
    public sealed class AddonManifestDocumentValidatorTests
    {
        private string _root;
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "sdk-manifest-document-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [TestCase("{\"id\":\"a\",\"id\":\"b\"}")]
        [TestCase("{} {}")]
        [TestCase("[]")]
        [TestCase("{\"unknown\":true}")]
        [TestCase("{\"contents\":[{\"map\":{\"unknown\":true}}]}")]
        [TestCase("{\"contents\":[{\"map\":{\"platforms\":{}}}]}")]
        [TestCase("{\"contents\":[{\"audio\":{\"files\":[{\"unknown\":1}]}}]}")]
        [TestCase("{\"id\":42}")]
        [TestCase("{\"schemaVersion\":\"9\"}")]
        [TestCase("{\"schemaVersion\":2147483648}")]
        [TestCase("{\"requires\":[42]}")]
        [TestCase("{\"contents\":[{\"map\":{\"platforms\":[0]}}]}")]
        [TestCase("{\"contents\":[{\"media\":{\"files\":[0]}}]}")]
        [TestCase("{\"contents\":[{\"media\":{\"files\":[{\"size\":\"1\"}]}}]}")]
        [TestCase("{\"contents\":[{\"media\":{\"files\":[{\"size\":9223372036854775808}]}}]}")]
        public void RejectsMalformedOrUnsupportedDocuments(string json)
        {
            File.WriteAllText(AddonHelper.GetManifestPath(_root), json);
            Assert.That(AddonManifestDocumentValidator.TryValidate(_root, out _), Is.False);
        }

        [Test]
        public void AcceptsCurrentManifestFields()
        {
            File.WriteAllText(AddonHelper.GetManifestPath(_root),
                "{\"contents\":[{\"kind\":\"map\",\"map\":{\"defaultModeId\":\"skirmish\",\"musicSet\":\"none\",\"platforms\":[{\"name\":\"windows\",\"files\":[{\"path\":\"a.bundle\",\"size\":1,\"sha256\":\"hash\"}]}]}}]}");
            Assert.That(AddonManifestDocumentValidator.TryValidate(_root, out var error), Is.True, error);
        }

        [Test]
        public void RejectsMissingEmptyAndOversizedDocuments()
        {
            Assert.That(AddonManifestDocumentValidator.TryValidate(_root, out _), Is.False);
            var path = AddonHelper.GetManifestPath(_root);
            File.WriteAllText(path, "");
            Assert.That(AddonManifestDocumentValidator.TryValidate(_root, out _), Is.False);
            using (var stream = File.Create(path)) stream.SetLength(AddonFilePolicy.MaximumManifestBytes + 1);
            Assert.That(AddonManifestDocumentValidator.TryValidate(_root, out _), Is.False);
        }
    }
}
