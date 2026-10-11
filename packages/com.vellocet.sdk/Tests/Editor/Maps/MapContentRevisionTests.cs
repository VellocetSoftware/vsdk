using NUnit.Framework;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class MapContentRevisionTests
    {
        [Test]
        public void RevisionSurvivesPlatformBuildsAndResealingButChangesWithContent()
        {
            var receipt = new MapCompilationReceipt {
                mapId = "author.city", compilerVersion = "2.0.0", sourceDependenciesHash = "source-a",
                vsigProgramHash = "script-a", createdUtc = "first-build"
            };
            var revision = MapCompilationReceiptUtility.ComputeContentRevision(receipt);
            receipt.createdUtc = "second-build";
            receipt.buildId = "linux-server";
            receipt.platforms = new[] { new MapCompilationReceipt.PlatformArtifact { name = "Linux" } };
            MapCompilationReceiptUtility.Seal(receipt);
            Assert.That(MapCompilationReceiptUtility.ComputeContentRevision(receipt), Is.EqualTo(revision));
            receipt.sourceDependenciesHash = "source-b";
            Assert.That(MapCompilationReceiptUtility.ComputeContentRevision(receipt), Is.Not.EqualTo(revision));
            receipt.sourceDependenciesHash = "source-a";
            receipt.vsigProgramHash = "script-b";
            Assert.That(MapCompilationReceiptUtility.ComputeContentRevision(receipt), Is.Not.EqualTo(revision));
        }

        [TestCase(6)]
        [TestCase(7)]
        public void LegacyReceiptSealsDoNotDependOnNewRevisionFields(int schema)
        {
            var receipt = new MapCompilationReceipt { schemaVersion = schema, mapId = "author.city" };
            MapCompilationReceiptUtility.Seal(receipt);
            receipt.sourceDependenciesHash = "new-field";
            receipt.contentRevision = "new-revision";
            Assert.That(MapCompilationReceiptUtility.HasValidSeal(receipt), Is.True);
        }
    }
}
