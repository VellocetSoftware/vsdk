// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vellocet.Sdk.Editor.Maps;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class MapLightingBakeDiagnosticsTests
    {
        [TestCase("Non finite value in lightmapper output")]
        [TestCase("d3d11: failed to create buffer [0x887A0005]")]
        [TestCase("D3D shader create error [0x887a0006]")]
        [TestCase("Graphics device reset [0x887A0007]")]
        public void InvalidOutputAndDeviceFailuresRejectTheBake(string message)
        {
            using var diagnostics = new MapLightingBakeDiagnosticsUtility.LightmapperLogScope();
            Debug.LogWarning(message);
            Assert.That(diagnostics.HasFailure, Is.True);
            Assert.That(diagnostics.BuildFailureMessage(), Does.Contain(message));
        }

        [Test]
        public void FailedProbeReadbackRejectsTheBake()
        {
            using var diagnostics = new MapLightingBakeDiagnosticsUtility.LightmapperLogScope();
            const string message = "Cannot access the data as it is not available";
            LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape(message)));
            Debug.LogException(new InvalidOperationException(message));
            Assert.That(diagnostics.HasFailure, Is.True);
            Assert.That(diagnostics.BuildFailureMessage(), Does.Contain(message));
        }

        [Test]
        public void ProgressMessagesAndUnrelatedWarningsDoNotRejectTheBake()
        {
            using var diagnostics = new MapLightingBakeDiagnosticsUtility.LightmapperLogScope();
            Debug.Log("Baking lightmaps");
            Debug.LogWarning("A new SDK version is available");
            Assert.That(diagnostics.HasFailure, Is.False);
        }
    }
}
