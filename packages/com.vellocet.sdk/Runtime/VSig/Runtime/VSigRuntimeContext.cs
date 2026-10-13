// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vertex.VSig;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Logging;
using Portable = Vertex.VSig;
using PortableRuntime = Vertex.VSig.Runtime;

namespace Vellocet.Sdk.VSig.Runtime
{
    public readonly struct VSigRuntimeStateEntry
    {
        public VSigRuntimeStateEntry(string name, string typeName, VSigValue value)
        { Name = name; TypeName = typeName; Value = value; }
        public string Name { get; }
        public string TypeName { get; }
        public VSigValue Value { get; }
    }

    /// <summary>Compatibility bridge for SDK/game callers. Vertex owns all script execution.</summary>
    public sealed partial class VSigRuntimeContext : IDisposable
    {
        public delegate bool AuthorityResolver(IEntitySignalRuntime runtime, string authority);
        public delegate bool ConsoleVariableResolver(string name, out string value, out string error);
        public delegate bool GlobalAuthorityResolver(string authority);
        public delegate bool RuntimeAvailabilityResolver(EntityMarker marker, string authority);
        public delegate bool ServerCommandResolver(int sceneHandle, int sourceLine, string command, out string error);

        // Registration defaults are captured by each map; replacing a bridge never changes an active map.
        public static ConsoleVariableResolver ResolveConsoleVariable { get; set; }
        public static ServerCommandResolver SubmitServerCommand { get; set; }
        public static AuthorityResolver ResolveAuthority { get; set; }
        public static GlobalAuthorityResolver ResolveGlobalAuthority { get; set; }
        public static RuntimeAvailabilityResolver IsRuntimeExpectedForAuthority { get; set; }

        private readonly GlobalAuthorityResolver _globalAuthority;
        private readonly List<VSigDiagnostic> _diagnostics = new();
        private VSigSceneSnapshot _scene;
        private int _reportedDiagnostics;
        private int _loggedDiagnostics;
        private bool _disposed;
        private Vertex.Unity.VertexTraceSession _trace;
        public string TracePath => _trace?.FilePath;
        public VSigProgram Program { get; }
        public PortableRuntime.VSigRuntimeContext PortableContext { get; private set; }
        public int SceneHandle { get; }
        public Guid MapId => _scene?.MapId ?? Guid.Empty;

        public VSigRuntimeContext(VSigProgram program, IEnumerable<EntityMarker> sceneMarkers, MonoBehaviour host)
            : this(program, null, sceneMarkers, host) { }

        internal VSigRuntimeContext(Portable.VSigCompiledProgram compiled, IEnumerable<EntityMarker> sceneMarkers,
            VSigSceneRuntime host) : this(null, compiled, sceneMarkers, host) { }

        private VSigRuntimeContext(VSigProgram program, Portable.VSigCompiledProgram compiled,
            IEnumerable<EntityMarker> sceneMarkers, MonoBehaviour host)
        {
            Program = program ?? new VSigProgram();
            SceneHandle = host ? host.gameObject.scene.handle : 0;
            var authority = ResolveAuthority;
            _globalAuthority = ResolveGlobalAuthority;
            var availability = IsRuntimeExpectedForAuthority;
            var variables = ResolveConsoleVariable;
            var commands = SubmitServerCommand;
            var capabilities = new PortableRuntime.VSigHost
            {
                Globals = VSigGlobalEvents.Catalog,
                ResolveAuthority = (runtime, side) => runtime is VSigSceneSnapshot.Runtime adapter
                    ? authority?.Invoke(adapter.Source, side) ?? true : _globalAuthority?.Invoke(side) ?? true,
                IsRuntimeExpectedForAuthority = (entity, side) =>
                    availability?.Invoke(_scene.Entities.First(pair => pair.Value.Equals(entity)).Key, side) ?? true,
                SignalEmitted = (source, output, values) =>
                    VSigRuntimeRegistry.PublishSignal(source, output, ToSdk(values), SceneHandle)
            };
            if (variables != null) capabilities.ResolveConsoleVariable =
                (string name, out string value, out string error) => variables(name, out value, out error);
            if (commands != null) capabilities.SubmitServerCommand =
                (Portable.VSigSourceSpan source, string command, out string error) =>
                    commands(SceneHandle, source.Line, command, out error);

            try
            {
                _deferred = DeferExecution?.Invoke(SceneHandle) == true;
                var markers = (sceneMarkers ?? Array.Empty<EntityMarker>()).Where(marker => marker &&
                    (!host || marker.gameObject.scene.handle == SceneHandle)).ToArray();
                var sceneHost = host as VSigSceneRuntime;
                var importedMapId = sceneHost ? Guid.Empty : VSigSceneSnapshot.ImportedMapId(markers);
                _scene = new VSigSceneSnapshot(sceneHost ? sceneHost.EnsureMapId(markers) : importedMapId != Guid.Empty ? importedMapId : Guid.NewGuid(), markers,
                    sceneHost ? sceneHost.GetNodeId : null, sceneHost && sceneHost.IdentityBaked ? sceneHost.GetNodeName : null);
                if (compiled == null)
                    compiled = Portable.VSigCompiledProgram.FromProgram(Program);
                var runtimeProgram = compiled.ToProgram();
                if (program == null) Program = runtimeProgram;
                RefreshAuthority(capabilities);
                PortableContext = new PortableRuntime.VSigRuntimeContext(runtimeProgram,
                    _scene.Entities.Values, capabilities);
            }
            catch (Exception exception)
            {
                Program = new VSigProgram();
                _diagnostics.Add(new VSigDiagnostic(VSigDiagnosticSeverity.Error, 0, "VSIG_PROGRAM_REJECTED", exception.Message));
                PortableContext = new PortableRuntime.VSigRuntimeContext(new Portable.VSigProgram(), null, capabilities);
            }
            _trace = Vertex.Unity.VertexTraceSession.TryStart(PortableContext, MapId, host);
            ReportDiagnostics();
        }

        public IReadOnlyList<VSigDiagnostic> Diagnostics
        {
            get
            {
                while (_reportedDiagnostics < PortableContext.Diagnostics.Count)
                {
                    _diagnostics.Add(PortableContext.Diagnostics[_reportedDiagnostics++]);
                }
                return _diagnostics;
            }
        }
        public IReadOnlyList<string> EventTrace => PortableContext.EventTrace.Select(entry => entry.ToString()).ToArray();
        public IReadOnlyList<string> GetActiveTasks() => PortableContext.GetActiveTasks();
        public IReadOnlyList<VSigRuntimeStateEntry> GetStateSnapshot() => PortableContext.GetStateSnapshot()
            .Select(entry => new VSigRuntimeStateEntry(entry.Name, entry.TypeName, VSigValue.FromObject(entry.Value.Value))).ToArray();
        public bool TryGetVariable(string name, out VSigValue value)
        {
            var found = PortableContext.TryGetVariable(name, out var result);
            value = VSigValue.FromObject(result.Value);
            return found;
        }
        public bool TryGetModuleState(string instanceAlias, string stateName, out VSigValue value)
        {
            var found = PortableContext.TryGetModuleState(instanceAlias, stateName, out var result);
            value = VSigValue.FromObject(result.Value);
            return found;
        }
        public void Advance(double seconds)
        {
            if (_disposed || _deferred) return;
            RefreshAuthority(PortableContext.Host);
            PortableContext.Advance(seconds);
            ReportDiagnostics();
            _trace?.Tick();
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearDeferred();
            _trace?.Dispose();
            PortableContext.Dispose();
        }
        public void OnRuntimeRegistered(EntityMarker marker, IEntitySignalRuntime runtime)
        {
            if (_disposed || !marker || runtime == null || _scene == null ||
                !_scene.Entities.TryGetValue(marker, out var entity)) return;
            if (!runtime.SignalHandle.IsEmpty && !runtime.SignalHandle.Equals(EntitySignalHandle.ForEntity(marker)))
                throw new ArgumentException("SDK runtime identity does not match its marker.");
            RegisterDeferredSource(marker, runtime);
            PortableContext.OnRuntimeRegistered(entity, new VSigSceneSnapshot.Runtime(_scene, entity, runtime));
        }
        public void OnRuntimeUnregistered(EntityMarker marker)
        {
            if (!ReferenceEquals(marker, null)) _startupSources?.Remove(marker);
            if (_scene != null && !ReferenceEquals(marker, null) && _scene.Entities.TryGetValue(marker, out var entity))
                PortableContext.OnRuntimeUnregistered(entity);
        }
        public void HandleEmit(EntityMarker marker, string output, string parameter) =>
            HandleEmit(marker, output, parameter, default);
        public void HandleEmit(EntityMarker marker, string output, string parameter, EntitySignalContext context)
        {
            if (_disposed || !marker || _scene == null || !_scene.Entities.TryGetValue(marker, out var entity)) return;
            if (_deferred) { DeferEntity(marker, output, parameter, context); return; }
            DispatchEntity(marker, output, parameter, context);
        }
        private void DispatchEntity(EntityMarker marker, string output, string parameter, EntitySignalContext context)
        {
            if (!marker || !_scene.Entities.TryGetValue(marker, out var entity)) return;
            RefreshAuthority(PortableContext.Host);
            PortableContext.HandleEmit(entity, output, parameter, _scene.ToVertex(context));
            ReportDiagnostics();
        }
        public void HandleGlobalEmit(string source, string output, IReadOnlyList<VSigValue> values = null)
        {
            if (_disposed || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(output)) return;
            if (_deferred) { DeferGlobal(source, output, values); return; }
            DispatchGlobal(source, output, values);
        }
        private void DispatchGlobal(string source, string output, IReadOnlyList<VSigValue> values)
        {
            RefreshAuthority(PortableContext.Host);
            // Mod-to-mod observation remains an SDK bus. Undeclared events never enter the script VM.
            if (!VSigGlobalEvents.Catalog.TryGetSource(source, out var signals) ||
                !signals.ContainsKey(EntitySignalNameUtility.Normalize(output)))
            {
                VSigRuntimeRegistry.PublishSignal(source, EntitySignalNameUtility.Normalize(output), values, SceneHandle);
                return;
            }
            PortableContext.HandleGlobalEmit(source, output,
                values?.Select(value => PortableRuntime.VSigValue.FromObject(value.Value)).ToArray());
            ReportDiagnostics();
        }
        private void ReportDiagnostics()
        {
            var diagnostics = Diagnostics;
            while (_loggedDiagnostics < diagnostics.Count)
            {
                var item = diagnostics[_loggedDiagnostics++];
                if (item.Severity == VSigDiagnosticSeverity.Error) SdkConsoleBridge.Error("[VSig] " + item);
                else if (item.Severity == VSigDiagnosticSeverity.Warning) SdkConsoleBridge.Warn("[VSig] " + item);
                else SdkConsoleBridge.Write("[VSig] " + item);
            }
        }
        private void RefreshAuthority(PortableRuntime.VSigHost host)
        {
            try
            {
                host.Authority = (_globalAuthority?.Invoke(VSigAuthority.Server) ?? true
                    ? PortableRuntime.VSigHostAuthority.Server : PortableRuntime.VSigHostAuthority.None) |
                    (_globalAuthority?.Invoke(VSigAuthority.Client) ?? true
                    ? PortableRuntime.VSigHostAuthority.Client : PortableRuntime.VSigHostAuthority.None);
            }
            catch (Exception exception)
            {
                host.Authority = PortableRuntime.VSigHostAuthority.None;
                if (!_diagnostics.Any(diagnostic => diagnostic.Code == "VSIG_AUTHORITY_RESOLVER_FAILED"))
                    _diagnostics.Add(new VSigDiagnostic(VSigDiagnosticSeverity.Error, 0,
                        "VSIG_AUTHORITY_RESOLVER_FAILED", exception.Message));
            }
        }
        private static VSigValue[] ToSdk(IReadOnlyList<PortableRuntime.VSigValue> values) =>
            values?.Select(value => VSigValue.FromObject(value.Value)).ToArray();
    }
}
