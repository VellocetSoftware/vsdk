using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Aneurism.Tests
{
    public sealed class AneurismSdkTests
    {
        [TestCase("Assets/Art/Models/entities/door.prefab", false)]
        [TestCase("Assets/Art/Models/entities/door.prefab.meta", false)]
        [TestCase("Assets/Art/Models/entities/private.asset", false)]
        [TestCase("Assets/Art/Models/entities/door.fbx", true)]
        [TestCase("Assets/Art/Materials/door.mat", true)]
        [TestCase("Assets/MapAuthoring/Kits/Door.prefab", true)]
        public void OnlyPublicArtAndAuthoringKitsEnterTheSdk(string path, bool allowed)
        {
            Assert.That(SdkContentPathUtility.TryMapSourceToInstalledPath(path, out var installed), Is.EqualTo(allowed));
            Assert.That(SdkContentPathUtility.IsSourceSdkAssetPath(path), Is.EqualTo(allowed));
            if (allowed) Assert.That(SdkContentPathUtility.TryMapInstalledToSourcePath(installed, out var source) && source == path, Is.True);
        }

        [Test]
        public void AuthoringUsesTheExistingLocalAndNetworkPlayerLayers()
        {
            Assert.That(SdkGame.Current.Id, Is.EqualTo(AneurismSdk.GameId));
            Assert.That(LayerMaskDefaults.TryGetPawnMask(out var mask), Is.True);
            Assert.That(mask.value, Is.EqualTo((1 << 3) | (1 << 8)));
            Assert.That(SdkGame.Current.ResolveLayers(SdkLayerSet.Item | SdkLayerSet.Ragdoll),
                Is.EqualTo((1 << 11) | (1 << 10) | (1 << 15)));
        }

        [Test]
        public void GameProfileDoesNotRequireGrimwarAuthoringTypes()
        {
            var sdk = typeof(EntityMarker).Assembly;
            Assert.That(sdk.GetType("Vellocet.Sdk.Entities.BlitzTankMarker"), Is.Null);
            Assert.That(sdk.GetType("Vellocet.Sdk.Entities.CapturePointMarker"), Is.Null);
            Assert.That(sdk.GetType("Vellocet.Sdk.VSig.VSigGlobalEvents+Gravewar"), Is.Null);
            Assert.That(VSig.VSigGlobalEvents.Catalog.Signals.Any(signal => signal.Source == "gravewar"), Is.False);
            Assert.That(Addons.AddonMedia.AudioType, Is.EqualTo("stereoform.event"));
            Assert.That(SdkGame.Current.SupportsExternalMods, Is.False);
            Assert.That(ModeIdPolicy.SafeFallbackModeId, Is.EqualTo("aneurism"));
            Assert.That(Maps.MapMusicSets.Name(default), Is.EqualTo("Unassigned"));
            Assert.That(Maps.MapMusicSets.TryParse("Compound", out _), Is.False);
            Assert.That(Maps.MapMusicSets.TryParse(null, out _), Is.True);
            Assert.That(SdkGame.Current.RequiresPackageGameId, Is.True);
        }
    }
}
