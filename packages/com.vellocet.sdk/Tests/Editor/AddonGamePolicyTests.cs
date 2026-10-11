using System.Collections.Generic;
using NUnit.Framework;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class AddonGamePolicyTests
    {
        private sealed class MapOnlyGame : SdkGameDefinition
        {
            public override string Id => "com.vellocet.aneurism-iv";
            public override string DisplayName => "ANEURISM IV";
            public override string DefaultModeId => "aneurism";
            public override bool SupportsExternalMods => false;
            public override IReadOnlyList<ModeDescriptor> Modes => System.Array.Empty<ModeDescriptor>();
            public override SdkAssetRootMapping[] AssetRoots => System.Array.Empty<SdkAssetRootMapping>();
            public override int ResolveLayers(SdkLayerSet layers) => 0;
            public override int NavigationMask => 0;
        }

        [Test]
        public void GameIdentityAndMapOnlyPolicyAreCheckedBeforeMounting()
        {
            var game = new MapOnlyGame();
            var manifest = new AddonManifest { gameId = game.Id,
                contents = new[] { new AddonManifest.ContentEntry { kind = AddonManifest.MapContentKind } } };
            Assert.That(AddonGamePolicy.TryValidate(manifest, game, out _), Is.True);
            manifest.gameId = null;
            Assert.That(AddonGamePolicy.TryValidate(manifest, game, out _), Is.False);
            manifest.gameId = "com.vellocet.grimwar";
            Assert.That(AddonGamePolicy.TryValidate(manifest, game, out _), Is.False);
            manifest.gameId = game.Id;
            manifest.contents[0].kind = "MODS";
            Assert.That(AddonGamePolicy.TryValidate(manifest, game, out _), Is.False);
            manifest.contents[0].kind = AddonManifest.MapContentKind;
            manifest.contents[0].mod = new AddonManifest.ModContent();
            Assert.That(AddonGamePolicy.TryValidate(manifest, game, out _), Is.False);
        }

        [TestCase("mods/server.DLL", true)]
        [TestCase("media/native.so", true)]
        [TestCase("scripts/custom.cs", true)]
        [TestCase("scripts/map.vsig", false)]
        [TestCase("maps/city/content.bundle", false)]
        public void ExecutableFilesCannotHideAmongMapResources(string path, bool forbidden) =>
            Assert.That(AddonGamePolicy.IsCodeFile(path), Is.EqualTo(forbidden));
    }
}
