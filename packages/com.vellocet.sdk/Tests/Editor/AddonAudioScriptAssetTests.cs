// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Audio;

namespace Vellocet.Sdk.Tests
{
    public sealed class AddonAudioScriptAssetTests
    {
        [Test]
        public void AuthoringAssetTypes_HaveMatchingMonoScripts()
        {
            AssertHasMatchingMonoScript<AddonAudioCue>();
            AssertHasMatchingMonoScript<AddonAudioCollection>();
        }

        private static void AssertHasMatchingMonoScript<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            try
            {
                var script = MonoScript.FromScriptableObject(instance);
                Assert.That(script, Is.Not.Null, $"{typeof(T).Name} does not have a Unity script asset.");
                Assert.That(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(script)),
                    Is.EqualTo(typeof(T).Name));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}