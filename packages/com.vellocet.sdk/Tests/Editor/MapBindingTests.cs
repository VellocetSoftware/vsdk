using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vellocet.Sdk.Maps;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class MapBindingTests
    {
        private sealed class SoundSettings
        {
            public AudioClip[] clips = new AudioClip[2];
            [SerializeField] private List<AudioClip> alternates = new() { null };
            [NonSerialized] public AudioClip transient;
            public AudioClip Property { get; set; }
            public AudioClip Alternate => alternates[0];
        }

        [Test]
        public void SerializedAudioArraysAndListsSupportValidationAndAssignment()
        {
            var settings = new SoundSettings();
            var clip = AudioClip.Create("Shared clip", 1, 1, 44100, false);
            var texture = new Texture2D(1, 1);
            try
            {
                Assert.That(MapBinding.TryAssign(settings, "clips.Array.data[1]", clip, apply: false), Is.True);
                Assert.That(settings.clips[1], Is.Null);
                Assert.That(MapBinding.TryAssign(settings, "clips.Array.data[1]", clip), Is.True);
                Assert.That(settings.clips[0], Is.Null);
                Assert.That(settings.clips[1], Is.SameAs(clip));
                Assert.That(MapBinding.TryAssign(settings, "alternates.Array.data[0]", clip), Is.True);
                Assert.That(settings.Alternate, Is.SameAs(clip));
                Assert.That(MapBinding.TryAssign(settings, "clips.Array.data[0]", texture), Is.False);
            }
            finally { Object.DestroyImmediate(clip); Object.DestroyImmediate(texture); }
        }

        [TestCase("clips.Array.data[-1]")]
        [TestCase("clips.Array.data[2]")]
        [TestCase("clips.Array.data[2147483648]")]
        [TestCase("clips.Array.data[+0]")]
        [TestCase("clips.Array.data[0].name")]
        [TestCase("clips.Array.data[]")]
        [TestCase("clips.Array.data[0][")]
        [TestCase("clips.Array.data[0")]
        [TestCase("Property")]
        [TestCase("transient")]
        [TestCase("alternates.Count")]
        public void MalformedOrUnserializedPathsCannotChangeTheTarget(string path)
        {
            var settings = new SoundSettings();
            var clip = AudioClip.Create("Shared clip", 1, 1, 44100, false);
            try
            {
                Assert.That(MapBinding.TryAssign(settings, path, clip), Is.False);
                Assert.That(settings.clips, Is.EqualTo(new AudioClip[2]));
                Assert.That(settings.Alternate, Is.Null);
                Assert.That(settings.Property, Is.Null);
                Assert.That(settings.transient, Is.Null);
            }
            finally { Object.DestroyImmediate(clip); }
        }
    }
}
