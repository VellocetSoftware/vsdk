// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Text.RegularExpressions;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Vellocet.Sdk.Entities;
using Object = UnityEngine.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vellocet.Sdk.Editor.Maps;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class MapLightingBakeDiagnosticsTests
    {
        [Test]
        public void SuppressesOnlyPrefabLightsThatWereBaked()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var previousResolver = MapPrefabLighting.ResolvePrefab;
            var template = new GameObject("Template");
            var placement = new GameObject("Placement");
            SceneManager.MoveGameObjectToScene(placement, scene);
            var marker = placement.AddComponent<LabelMarker>();
            placement.transform.position = new Vector3(3, 4, 5);

            try
            {
                var baked = AddLight(LightmapBakeType.Baked);
                baked.transform.localPosition = Vector3.right;
                var mixed = AddLight(LightmapBakeType.Mixed);
                var realtime = AddLight(LightmapBakeType.Realtime);
                var unbaked = AddLight(LightmapBakeType.Baked);
                MapPrefabLighting.ResolvePrefab = entity => entity == marker
                    ? (template, placement.transform.localToWorldMatrix)
                    : default;

                Assert.That(marker.SuppressBakedPrefabLights(template), Is.Null, "Legacy scenes keep their lights.");
                var pending = MapPrefabLighting.Prepare(scene);
                var lights = pending[marker];
                Assert.That(lights[0].transform.position, Is.EqualTo(new Vector3(4, 4, 5)));
                Assert.That(lights[1], Is.Null);
                Assert.That(lights[2], Is.Null);
                lights[0].bakingOutput = new LightBakingOutput
                {
                    isBaked = true, lightmapBakeType = LightmapBakeType.Baked
                };
                Assert.That(MapPrefabLighting.Complete(pending), Is.EqualTo(1));
                Assert.That(marker.SuppressBakedPrefabLights(template), Is.EquivalentTo(new[] { baked }));
                Assert.That(baked.enabled, Is.False);
                Assert.That(mixed.enabled && realtime.enabled && unbaked.enabled, Is.True);
            }
            finally
            {
                MapPrefabLighting.ResolvePrefab = previousResolver;
                Object.DestroyImmediate(template);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            Light AddLight(LightmapBakeType type)
            {
                var host = new GameObject(type.ToString());
                host.transform.SetParent(template.transform, false);
                var light = host.AddComponent<Light>();
                light.lightmapBakeType = type;
                return light;
            }
        }

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
