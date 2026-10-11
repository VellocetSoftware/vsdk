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
    // Language semantics are covered by Vertex; these checks exercise the SDK host and Unity adapters.
    public sealed partial class VSigSdkIntegrationTests
    {
        private VSigRuntimeContext.GlobalAuthorityResolver _previousAuthority;
        private VSigRuntimeContext.AuthorityResolver _previousEntityAuthority;

        [SetUp]
        public void SetUp()
        {
            _previousAuthority = VSigRuntimeContext.ResolveGlobalAuthority;
            _previousEntityAuthority = VSigRuntimeContext.ResolveAuthority;
            VSigRuntimeContext.ResolveAuthority = (_, _) => true;
            VSigRuntimeContext.ResolveGlobalAuthority = _ => true;
        }

        [TearDown]
        public void TearDown()
        {
            VSigRuntimeContext.ResolveGlobalAuthority = _previousAuthority;
            VSigRuntimeContext.ResolveAuthority = _previousEntityAuthority;
        }

        [Test]
        public void Runtime_RejectsProgramsThatExceedSecurityLimits()
        {
            var program = new VSigProgram();
            for (var index = 0; index <= VSigSecurityLimits.MaximumRefs; index++)
                program.Refs.Add(new VSigRefDeclaration());

            LogAssert.Expect(LogType.Error, new Regex("VSIG_PROGRAM_REJECTED"));
            using var runtime = new VSigRuntimeContext(program, null, null);

            Assert.That(runtime.Program.Refs, Is.Empty);
            Assert.That(runtime.Diagnostics.Any(item => item.Code == "VSIG_PROGRAM_REJECTED"), Is.True);
        }

        [Test]
        public void Runtime_CapsHandlerBurstsWithinOneFrame()
        {
            var program = new VSigProgram();
            for (var index = 0; index <= VSigSecurityLimits.MaximumHandlerStartsPerFrame; index++)
                program.Handlers.Add(new VSigEventHandler
                {
                    Line = 1,
                    Authority = VSigAuthority.Server,
                    SourceAlias = "map",
                    OutputName = "ready"
                });

            using var runtime = new VSigRuntimeContext(program, null, null);
            runtime.HandleGlobalEmit("map", "ready");

            Assert.That(runtime.Diagnostics.Any(item => item.Code == "VSIG_HANDLER_RATE_LIMIT"), Is.True);
        }

        [Test]
        public void Runtime_ExecutesModuleInputOutputAndNestedControlFlow()
        {
            const string source = @"
seed 17
var observed: float = 0
var branch: string = ""none""

module counter(max: float = 3):
    state value: float = 0
    output changed(value: float)

    input add(amount: float = 1):
        value = min(value + amount, max)
        if value >= max:
            emit changed(value)
        else:
            emit changed(-1)

use counter as captures(max = 2)

on captures.changed(value: float):
    observed = value
    match value:
        case 2:
            branch = ""max""
        default:
            branch = ""other""

on map.ready:
    captures.add(2)
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);
            Assert.That(diagnostics, Is.Empty);

            using var runtime = new VSigRuntimeContext(program, null, null);
            runtime.HandleGlobalEmit("map", "ready");

            Assert.That(runtime.TryGetModuleState("captures", "value", out var state), Is.True);
            Assert.That(state.ToString(), Is.EqualTo("2"));
            Assert.That(runtime.TryGetVariable("observed", out var observed), Is.True);
            Assert.That(observed.ToString(), Is.EqualTo("2"));
            Assert.That(runtime.TryGetVariable("branch", out var branch), Is.True);
            Assert.That(branch.ToString(), Is.EqualTo("max"));
        }

        [Test]
        public void Runtime_ModuleOutputHandlerNormalizesMixedCaseInstanceAlias()
        {
            const string source = @"
var observed: float = 0

module counter:
    output changed(value: float)

    input add(value: float):
        emit changed(value)

use counter as DamageCounter

on DamageCounter.changed(value: float):
    observed = value

on map.ready:
    DamageCounter.add(7)
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics, Is.Empty);
            using var runtime = new VSigRuntimeContext(program, null, null);
            runtime.HandleGlobalEmit("map", "ready");

            Assert.That(runtime.TryGetVariable("observed", out var observed), Is.True);
            Assert.That(observed.ToString(), Is.EqualTo("7"));
            Assert.That(runtime.Diagnostics, Is.Empty);
        }

        [Test]
        public void ServerCommand_ParsesValidatesRoundTripsAndExecutesThroughHostBridge()
        {
            const string source = @"
var gravity: float = -6

once on map.ready:
    server_command(format(""sv_gravity {0}"", gravity))
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics, Is.Empty);
            Assert.That(program.Handlers[0].Statements[0], Is.TypeOf<VSigServerCommandStatement>());

            var restored = VSigCompiledProgram.FromProgram(program).ToProgram();
            Assert.That(restored.Handlers[0].Statements[0], Is.TypeOf<VSigServerCommandStatement>());

            var previousAuthorityResolver = VSigRuntimeContext.ResolveGlobalAuthority;
            var previousCommandResolver = VSigRuntimeContext.SubmitServerCommand;
            var invocations = 0;
            var observedSceneHandle = -1;
            var observedLine = -1;
            string observedCommand = null;

            try
            {
                VSigRuntimeContext.ResolveGlobalAuthority = _ => true;
                VSigRuntimeContext.SubmitServerCommand =
                    (int sceneHandle, int sourceLine, string command, out string error) =>
                    {
                        invocations++;
                        observedSceneHandle = sceneHandle;
                        observedLine = sourceLine;
                        observedCommand = command;
                        error = null;
                        return true;
                    };

                using var runtime = new VSigRuntimeContext(restored, null, null);
                runtime.HandleGlobalEmit("map", "ready");

                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(observedSceneHandle, Is.Zero);
                Assert.That(observedLine, Is.GreaterThan(0));
                Assert.That(observedCommand, Is.EqualTo("sv_gravity -6"));
                Assert.That(runtime.Diagnostics, Is.Empty);
            }
            finally
            {
                VSigRuntimeContext.SubmitServerCommand = previousCommandResolver;
                VSigRuntimeContext.ResolveGlobalAuthority = previousAuthorityResolver;
            }
        }

        [Test]
        public void Validator_RejectsServerCommandWithAnythingOtherThanOneExpression()
        {
            const string source = @"
on map.ready:
    server_command()
    server_command(""say first"", ""say second"")
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics.Count(diagnostic =>
                diagnostic.Code == "VSIG_SERVER_COMMAND_ARGUMENT_COUNT"), Is.EqualTo(2));
        }

        [Test]
        public void ProceduralEntityTargets_ParseValidateAndRoundTripWithoutStaticRefs()
        {
            const string source = @"
on map.ready:
    !activator.kill
    !caller.disable()
    !self.toggle
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics, Is.Empty);
            var calls = program.Handlers[0].Statements.Cast<VSigCallStatement>().ToArray();
            Assert.That(calls.Select(call => call.TargetKind), Is.EqualTo(new[]
            {
                VSigEntityTargetKind.Activator,
                VSigEntityTargetKind.Caller,
                VSigEntityTargetKind.Self
            }));

            var restored = VSigCompiledProgram.FromProgram(program).ToProgram();
            var restoredCalls = restored.Handlers[0].Statements.Cast<VSigCallStatement>().ToArray();
            Assert.That(restoredCalls.Select(call => call.TargetKind),
                Is.EqualTo(calls.Select(call => call.TargetKind)));
            Assert.That(restoredCalls.Select(call => call.TargetAlias),
                Is.EqualTo(new[] { "!activator", "!caller", "!self" }));
        }

        [Test]
        public void Validator_RejectsUnknownProceduralEntityTarget()
        {
            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse("on map.ready:\n    !owner.kill\n", diagnostics);

            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics.Any(diagnostic =>
                diagnostic.Code == "VSIG_PROCEDURAL_TARGET_UNKNOWN"), Is.True);
        }

        [Test]
        public void Runtime_ProceduralTargetsResolveContextAndAdvanceInvocationProvenance()
        {
            var sourceObject = new GameObject("source");
            var activatorObject = new GameObject("activator");
            var callerObject = new GameObject("caller");

            try
            {
                var sourceMarker = sourceObject.AddComponent<LightMarker>();
                var activatorMarker = activatorObject.AddComponent<LightMarker>();
                var callerMarker = callerObject.AddComponent<LightMarker>();
                SetMarkerGuid(sourceMarker, Guid.NewGuid());
                SetMarkerGuid(activatorMarker, Guid.NewGuid());
                SetMarkerGuid(callerMarker, Guid.NewGuid());

                var markers = new EntityMarker[] { sourceMarker, activatorMarker, callerMarker };
                const string source = @"
ref source: env_light match source

on source.toggle:
    !activator.kill
    !caller.kill
    !self.kill
";
                var diagnostics = new List<VSigDiagnostic>();
                var program = VSigSceneCompiler.Parse(source, diagnostics, markers);
                VSigSceneCompiler.Validate(program, markers, diagnostics);
                Assert.That(diagnostics, Is.Empty);

                var sourceRuntime = new RecordingSignalRuntime(sourceMarker);
                var activatorRuntime = new RecordingSignalRuntime(activatorMarker);
                var callerRuntime = new RecordingSignalRuntime(callerMarker);
                using var runtime = new VSigRuntimeContext(program, markers, null);
                runtime.OnRuntimeRegistered(sourceMarker, sourceRuntime);
                runtime.OnRuntimeRegistered(activatorMarker, activatorRuntime);
                runtime.OnRuntimeRegistered(callerMarker, callerRuntime);

                var initial = new EntitySignalContext(
                    activatorRuntime.SignalHandle,
                    callerRuntime.SignalHandle,
                    sourceRuntime.SignalHandle);
                runtime.HandleEmit(sourceMarker, "toggle", null, initial);

                Assert.That(activatorRuntime.Invocations, Has.Count.EqualTo(1),
                    $"Trace: {string.Join(" | ", runtime.EventTrace)}; diagnostics: " +
                    string.Join(" | ", runtime.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
                Assert.That(callerRuntime.Invocations, Has.Count.EqualTo(1));
                Assert.That(sourceRuntime.Invocations, Has.Count.EqualTo(1));
                Assert.That(activatorRuntime.Invocations[0].Context.Activator,
                    Is.EqualTo(activatorRuntime.SignalHandle));
                Assert.That(activatorRuntime.Invocations[0].Context.Caller,
                    Is.EqualTo(sourceRuntime.SignalHandle));
                Assert.That(activatorRuntime.Invocations[0].Context.Self,
                    Is.EqualTo(activatorRuntime.SignalHandle));
                Assert.That(callerRuntime.Invocations[0].Context.Caller,
                    Is.EqualTo(sourceRuntime.SignalHandle));
                Assert.That(sourceRuntime.Invocations[0].Context.Self,
                    Is.EqualTo(sourceRuntime.SignalHandle));
                Assert.That(runtime.Diagnostics, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(activatorObject);
                Object.DestroyImmediate(callerObject);
            }
        }

        [Test]
        public void Runtime_SeedProducesDeterministicRandomSequence()
        {
            const string source = @"
seed 12345
var first: int = 0
var second: int = 0

on map.ready:
    first = random_int(0, 1000)
    second = random_int(0, 1000)
";

            var programA = VSigSceneCompiler.Parse(source, new List<VSigDiagnostic>());
            var programB = VSigSceneCompiler.Parse(source, new List<VSigDiagnostic>());
            using var runtimeA = new VSigRuntimeContext(programA, null, null);
            using var runtimeB = new VSigRuntimeContext(programB, null, null);

            runtimeA.HandleGlobalEmit("map", "ready");
            runtimeB.HandleGlobalEmit("map", "ready");

            runtimeA.TryGetVariable("first", out var firstA);
            runtimeA.TryGetVariable("second", out var secondA);
            runtimeB.TryGetVariable("first", out var firstB);
            runtimeB.TryGetVariable("second", out var secondB);
            Assert.That(firstA.ToString(), Is.EqualTo(firstB.ToString()));
            Assert.That(secondA.ToString(), Is.EqualTo(secondB.ToString()));
        }

        [Test]
        public void Runtime_EvaluatesArithmeticWithPrecedenceAndLeftAssociativity()
        {
            const string source = @"
var precedence: float = 2 + 3 * 4 - 8 / 2 + 5 % 2
var grouped: float = (2 + 3) * 4
var left_associative: float = 20 / 5 / 2
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics, Is.Empty);
            using var runtime = new VSigRuntimeContext(program, null, null);
            Assert.That(runtime.Diagnostics, Is.Empty);
            Assert.That(runtime.TryGetVariable("precedence", out var precedence), Is.True);
            Assert.That(runtime.TryGetVariable("grouped", out var grouped), Is.True);
            Assert.That(runtime.TryGetVariable("left_associative", out var leftAssociative), Is.True);
            Assert.That(precedence.ToString(), Is.EqualTo("11"));
            Assert.That(grouped.ToString(), Is.EqualTo("20"));
            Assert.That(leftAssociative.ToString(), Is.EqualTo("2"));
        }

        [Test]
        public void Validator_AcceptsConcreteEntityStateInitializedWithNull()
        {
            const string source = @"
var initial: DoorEntityState = null
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics, Is.Empty);
            using var runtime = new VSigRuntimeContext(program, null, null);
            Assert.That(runtime.Diagnostics, Is.Empty);
            Assert.That(runtime.TryGetVariable("initial", out var value), Is.True);
            Assert.That(value.Kind, Is.EqualTo(VSigValueKind.None));
        }

        [Test]
        public void Validator_RejectsUnknownValuesAndFunctions()
        {
            const string source = @"
var result: float = missing_value

on map.ready:
    result = mystery(1)
";

            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics.Any(diagnostic => diagnostic.Code == "VSIG_SYMBOL_UNKNOWN"), Is.True);
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Code == "VSIG_FUNCTION_UNKNOWN"), Is.True);
        }

        [Test]
        public void Validator_RequiresInitializersToUseAlreadyInitializedValues()
        {
            const string validSource = @"
const base_value: float = 2
const doubled: float = base_value * 2
var current: float = doubled
var next: float = current + 1
";
            const string invalidSource = @"
const forward_constant: float = later_constant
const later_constant: float = 2
var forward_variable: float = later_variable
var later_variable: float = later_constant
";

            var validDiagnostics = new List<VSigDiagnostic>();
            var validProgram = VSigSceneCompiler.Parse(validSource, validDiagnostics);
            VSigSceneCompiler.Validate(validProgram, null, validDiagnostics);

            var invalidDiagnostics = new List<VSigDiagnostic>();
            var invalidProgram = VSigSceneCompiler.Parse(invalidSource, invalidDiagnostics);
            VSigSceneCompiler.Validate(invalidProgram, null, invalidDiagnostics);

            Assert.That(validDiagnostics, Is.Empty);
            Assert.That(invalidDiagnostics.Count(diagnostic => diagnostic.Code == "VSIG_SYMBOL_UNKNOWN"),
                Is.EqualTo(2));
        }

        [Test]
        public void StandardLogicLibrary_EveryModuleExpandsAndValidates()
        {
            var libraryPath = StandardLogicLibraryPath();
            var libraryProgram = VSigSceneCompiler.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(libraryPath).text,
                new List<VSigDiagnostic>());
            var moduleNames = libraryProgram.Modules.Select(module => module.Name).ToArray();

            Assert.That(moduleNames, Is.Not.Empty);
            foreach (var moduleName in moduleNames)
            {
                var source = $"from \"{libraryPath}\" use {moduleName}\nuse {moduleName} as under_test\n";
                var diagnostics = new List<VSigDiagnostic>();
                var program = VSigSceneCompiler.Parse(source, diagnostics);
                VSigSceneCompiler.Validate(program, null, diagnostics);

                Assert.That(diagnostics, Is.Empty,
                    $"Standard module '{moduleName}' failed:\n{string.Join("\n", diagnostics)}");
            }
        }

        [Test]
        public void StandardTimer_ExposesIndexedTicksAndExplicitLifecycleInputs()
        {
            var libraryPath = StandardLogicLibraryPath();
            var source = $@"
from ""{libraryPath}"" use timer
use timer as clock(interval = 1s, times = 3)
";
            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics, Is.Empty);
            var tick = program.Outputs.Single(output =>
                output.InstanceAlias == "clock" && output.Name == "tick");
            Assert.That(tick.Parameters.Select(parameter => parameter.Name),
                Is.EqualTo(new[] { "index", "elapsed", "remaining" }));
            Assert.That(program.StateExports.Any(state =>
                state.InstanceAlias == "clock" && state.Name == "tick_index"), Is.True);
            Assert.That(program.Procedures.Where(procedure => procedure.InstanceAlias == "clock")
                    .Select(procedure => procedure.Name),
                Does.Contain("start").And.Contain("stop").And.Contain("restart")
                    .And.Not.Contain("pause").And.Not.Contain("resume"));

            var start = program.Procedures.Single(procedure =>
                procedure.InstanceAlias == "clock" && procedure.Name == "start");
            var runningGuard = start.Statements.OfType<VSigIfStatement>().Single();
            var remainingGuard = runningGuard.Then.OfType<VSigIfStatement>().Single();
            var schedule = remainingGuard.Else.OfType<VSigScheduleStatement>().Single();
            Assert.That(schedule.CountExpression, Is.EqualTo("clock_remaining"));

            var setTimes = program.Procedures.Single(procedure =>
                procedure.InstanceAlias == "clock" && procedure.Name == "set_times");
            Assert.That(setTimes.Statements.Last(), Is.TypeOf<VSigIfStatement>(),
                "Changing the configured repeat count must not corrupt an active schedule's remaining count.");
        }

        [Test]
        public void StandardTimer_FirePublishesDeterministicIndexedTicks()
        {
            var libraryPath = StandardLogicLibraryPath();
            var source = $@"
from ""{libraryPath}"" use timer

var observed_index: int = 0
var observed_remaining: int = 0

use timer as clock(interval = 1s, times = 3)

on clock.tick(index: int, elapsed: float, remaining: int):
    observed_index = index
    observed_remaining = remaining

on map.ready:
    clock.fire
    clock.fire
";
            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);
            Assert.That(diagnostics, Is.Empty);

            using var runtime = new VSigRuntimeContext(program, null, null);
            runtime.HandleGlobalEmit("map", "ready");

            Assert.That(runtime.TryGetVariable("observed_index", out var observedIndex), Is.True);
            Assert.That(observedIndex.ToString(), Is.EqualTo("2"));
            Assert.That(runtime.TryGetVariable("observed_remaining", out var observedRemaining), Is.True);
            Assert.That(observedRemaining.ToString(), Is.EqualTo("3"));
            Assert.That(runtime.TryGetModuleState("clock", "tick_index", out var tickIndex), Is.True);
            Assert.That(tickIndex.ToString(), Is.EqualTo("2"));
        }

        [Test]
        public void Validator_WarnsWhenTimerElapsedUsesExactEquality()
        {
            var libraryPath = StandardLogicLibraryPath();
            var source = $@"
from ""{libraryPath}"" use timer
use timer as clock

on clock.tick(index: int, seconds: float, remaining: int):
    if seconds == 10:
        clock.stop
    if index == 10:
        clock.stop
";
            var diagnostics = new List<VSigDiagnostic>();
            var program = VSigSceneCompiler.Parse(source, diagnostics);
            VSigSceneCompiler.Validate(program, null, diagnostics);

            Assert.That(diagnostics.Count(diagnostic =>
                diagnostic.Code == "VSIG_TIMER_ELAPSED_EQUALITY"), Is.EqualTo(1));
            Assert.That(diagnostics.Any(diagnostic =>
                    diagnostic.Severity == VSigDiagnosticSeverity.Error), Is.False,
                string.Join("\n", diagnostics));
        }

        private static string StandardLogicLibraryPath()
        {
            var package = PackageInfo.FindForAssembly(typeof(VSigParser).Assembly);
            Assert.That(package, Is.Not.Null);
            return Path.Combine(package.assetPath, "Runtime/VSig/Libraries/Logic.vsiglib")
                .Replace('\\', '/');
        }

        [Test]
        public void LogicScriptHost_ExpandsToAnOrdinaryModuleInstance()
        {
            var hostObject = new GameObject("Round Toggle");
            var library = new TextAsset(@"
module toggle(initial: bool = false):
    state value: bool = initial
    output changed(value: bool)

    input flip:
        value = not value
        emit changed(value)
");

            try
            {
                var host = hostObject.AddComponent<LogicScriptMarker>();
                SetAutoProperty(host, nameof(LogicScriptMarker.Library), library);
                SetAutoProperty(host, nameof(LogicScriptMarker.ModuleName), "toggle");
                SetAutoProperty(host, nameof(LogicScriptMarker.Alias), "round_toggle");

                var diagnostics = new List<VSigDiagnostic>();
                var program = VSigSceneCompiler.Parse(string.Empty, diagnostics,
                    new EntityMarker[] { host });
                VSigSceneCompiler.Validate(program, new EntityMarker[] { host }, diagnostics);

                Assert.That(diagnostics, Is.Empty);
                Assert.That(program.Procedures.Any(procedure =>
                    procedure.InstanceAlias == "round_toggle" && procedure.Name == "flip"), Is.True);
                Assert.That(program.StateExports.Any(state =>
                    state.InstanceAlias == "round_toggle" && state.Name == "value"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(library);
            }
        }

        private static void SetAutoProperty<T>(object target, string propertyName, T value)
        {
            var field = target.GetType().GetField($"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing serialized field for {propertyName}.");
            field.SetValue(target, value);
        }

        private static void SetMarkerGuid(EntityMarker marker, Guid value)
        {
            var field = typeof(EntityMarker).GetField("guid", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var reregister = marker.enabled && marker.gameObject.activeInHierarchy;
            if (reregister)
                marker.enabled = false;
            field.SetValue(marker, value.ToString("N"));
            if (reregister)
                marker.enabled = true;
        }

        private sealed class RecordingSignalRuntime : IEntitySignalRuntime
        {
            public RecordingSignalRuntime(EntityMarker marker)
            {
                Marker = marker;
            }

            private EntityMarker Marker { get; }
            public List<(string Input, EntitySignalContext Context)> Invocations { get; } = new();
            public EntitySignalContract SignalContract => Marker.SignalContract;
            public EntitySignalHandle SignalHandle => EntitySignalHandle.ForEntity(Marker);

            public bool InvokeInput(string inputName, string parameters, EntitySignalContext context)
            {
                Invocations.Add((inputName, context));
                return true;
            }

            public bool TryReadProperty(string propertyName, out object value)
            {
                value = null;
                return false;
            }

            public bool TryCaptureState(out IEntityStateSnapshot state)
            {
                state = null;
                return false;
            }

            public bool TryRestoreState(IEntityStateSnapshot state)
            {
                return false;
            }
        }
    }
}
