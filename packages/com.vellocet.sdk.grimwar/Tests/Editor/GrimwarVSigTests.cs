// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vertex.VSig;
using AssetDatabase = UnityEditor.AssetDatabase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.TestTools;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.VSig;
using VSigCompiledProgram = Vertex.VSig.VSigCompiledProgram;
using Vellocet.Sdk.VSig.Runtime;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed partial class VSigSdkIntegrationTests
    {
        [Test]
        public void GravewarGlobalEvents_ValidateAndDeliverTypedWavePayloads()
        {
            const string source = @"
var started_wave: int = 0
var zombie_count: int = 0
var ended_wave: int = 0
var end_result: string = ""none""

on gravewar.wave_started(wave: int, zombies: int):
    started_wave = wave
    zombie_count = zombies

on gravewar.wave_ended(wave: int, result: string):
    ended_wave = wave
    end_result = result

on gravewar.wave_started:
    started_wave = started_wave
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics, Is.Empty);
            using var runtime = new VSigRuntimeContext(program, null, null);
            runtime.HandleGlobalEmit(VSigGlobalEvents.Gravewar.Source,
                VSigGlobalEvents.Gravewar.WaveStarted,
                new[] { VSigValue.FromObject(7), VSigValue.FromObject(42) });
            runtime.HandleGlobalEmit(VSigGlobalEvents.Gravewar.Source,
                VSigGlobalEvents.Gravewar.WaveEnded,
                new[]
                {
                    VSigValue.FromObject(7),
                    VSigValue.FromString(VSigGlobalEvents.Gravewar.WaveEndResults.Cleared)
                });

            Assert.That(runtime.TryGetVariable("started_wave", out var startedWave), Is.True);
            Assert.That(startedWave.ToString(), Is.EqualTo("7"));
            Assert.That(runtime.TryGetVariable("zombie_count", out var zombieCount), Is.True);
            Assert.That(zombieCount.ToString(), Is.EqualTo("42"));
            Assert.That(runtime.TryGetVariable("ended_wave", out var endedWave), Is.True);
            Assert.That(endedWave.ToString(), Is.EqualTo("7"));
            Assert.That(runtime.TryGetVariable("end_result", out var endResult), Is.True);
            Assert.That(endResult.ToString(), Is.EqualTo(VSigGlobalEvents.Gravewar.WaveEndResults.Cleared));
            Assert.That(runtime.Diagnostics, Is.Empty);
        }

        [Test]
        public void GravewarGlobalEvents_RejectUnknownEventsAndWrongPayloadCounts()
        {
            const string source = @"
var observed: int = 0

on gravewar.wave_started(wave: int, zombies: int, extra: bool):
    observed = wave

on gravewar.wave_ended(wave: int, result: string, extra: bool):
    observed = wave

on gravewar.wave_paused:
    observed = 1
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics.Count(item => item.Code == "VSIG_HANDLER_ARGUMENT_COUNT"), Is.EqualTo(2));
            Assert.That(diagnostics.Count(item => item.Code == "VSIG_GLOBAL_OUTPUT_UNKNOWN"), Is.EqualTo(1));
        }

    }
}
