// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vertex.VSig;

namespace Vellocet.Sdk.VSig.Runtime
{
    public sealed partial class VSigRuntimeContext
    {
        /// <summary>Optional host policy, captured at construction. Standalone SDK contexts start immediately.</summary>
        public static Func<int, bool> DeferExecution { get; set; }
        public const int MaximumDeferredSignals = 4096;
        public const int MaximumDeferredPayloadBytes = 2 * 1024 * 1024;
        public bool IsDeferred => !_disposed && _deferred;

        private bool _deferred, _activating, _startupRejected;
        private int _sourceVersion;
        private long _deferredBytes;
        private Queue<DeferredSignal> _deferredSignals;
        private Dictionary<EntityMarker, (IEntitySignalRuntime Runtime, int Version)> _startupSources;

        private struct DeferredSignal
        {
            public EntityMarker Marker;
            public int SourceVersion;
            public string Source, Output, Parameter;
            public EntitySignalContext Context;
            public VSigValue[] Values;
            public long PayloadBytes;
        }

        private void RegisterDeferredSource(EntityMarker marker, IEntitySignalRuntime runtime)
        {
            if (!_deferred || _startupRejected) return;
            _startupSources ??= new();
            // Host server/client callbacks register the same owner twice.
            if (_startupSources.TryGetValue(marker, out var previous) && ReferenceEquals(previous.Runtime, runtime)) return;
            _startupSources[marker] = (runtime, ++_sourceVersion);
        }

        private void DeferEntity(EntityMarker marker, string output, string parameter, EntitySignalContext context)
        {
            if (_startupRejected || _startupSources == null || !_startupSources.TryGetValue(marker, out var source)) return;
            var bytes = 128L + TextBytes(output) + TextBytes(parameter) +
                HandleBytes(context.Activator) + HandleBytes(context.Caller) + HandleBytes(context.Self);
            if (!ReserveDeferred(bytes)) return;
            _deferredSignals.Enqueue(new DeferredSignal
            {
                Marker = marker, SourceVersion = source.Version, Output = output, Parameter = parameter,
                Context = context, PayloadBytes = bytes
            });
        }

        private void DeferGlobal(string source, string output, IReadOnlyList<VSigValue> values)
        {
            if (_startupRejected) return;
            var bytes = 128L + TextBytes(source) + TextBytes(output);
            // Count payload before copying it; queues cannot retain an unbounded caller-owned array.
            for (var i = 0; i < (values?.Count ?? 0); i++)
            {
                bytes += 16L + (values[i].Value is string text ? TextBytes(text) :
                    values[i].Kind == VSigValueKind.State ? TextBytes(Newtonsoft.Json.JsonConvert.SerializeObject(values[i].Value)) : 8L);
                if (bytes > MaximumDeferredPayloadBytes) break;
            }
            if (!ReserveDeferred(bytes)) return;
            var copy = values == null || values.Count == 0 ? Array.Empty<VSigValue>() : new VSigValue[values.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = values[i];
            _deferredSignals.Enqueue(new DeferredSignal
            { Source = source, Output = output, Values = copy, PayloadBytes = bytes });
        }

        private static long HandleBytes(EntitySignalHandle handle) => TextBytes(handle.SceneName) +
            TextBytes(handle.ScenePath) + TextBytes(handle.DisplayName) + TextBytes(handle.RuntimeIdentity);

        private static long TextBytes(string text) => (long)(text?.Length ?? 0) * sizeof(char);

        private bool ReserveDeferred(long bytes)
        {
            if ((_deferredSignals?.Count ?? 0) >= MaximumDeferredSignals ||
                bytes > MaximumDeferredPayloadBytes - _deferredBytes)
            {
                RejectStartup();
                return false;
            }
            _deferredSignals ??= new Queue<DeferredSignal>();
            _deferredBytes += bytes;
            return true;
        }

        /// <summary>Replay startup emissions after all runtimes and their initial state are ready. Safe to call twice.</summary>
        public void Activate()
        {
            if (_disposed || !_deferred || _activating || _startupRejected) return;
            _activating = true;
            try
            {
                var delivered = 0;
                while (!_disposed && !_startupRejected && _deferredSignals is { Count: > 0 })
                {
                    if (delivered++ >= MaximumDeferredSignals) { RejectStartup(); break; }
                    var signal = _deferredSignals.Dequeue();
                    _deferredBytes -= signal.PayloadBytes;
                    if (signal.SourceVersion != 0)
                    {
                        if (!signal.Marker || _startupSources == null ||
                            !_startupSources.TryGetValue(signal.Marker, out var current) ||
                            current.Version != signal.SourceVersion ||
                            current.Runtime is UnityEngine.Object owner && !owner) continue;
                        DispatchEntity(signal.Marker, signal.Output, signal.Parameter, signal.Context);
                    }
                    else DispatchGlobal(signal.Source, signal.Output, signal.Values);
                }
                if (!_startupRejected) _deferred = false;
            }
            finally
            {
                _activating = false;
                ClearDeferred();
            }
        }

        private void RejectStartup()
        {
            if (_startupRejected) return;
            _startupRejected = true;
            ClearDeferred();
            _diagnostics.Add(new VSigDiagnostic(VSigDiagnosticSeverity.Error, 0, "VSIG_STARTUP_LIMIT",
                "Deferred startup exceeds 4096 signals or 2 MiB of payload."));
            ReportDiagnostics();
        }

        private void ClearDeferred()
        {
            _deferredSignals?.Clear();
            _deferredSignals = null;
            _startupSources?.Clear();
            _startupSources = null;
            _deferredBytes = 0;
        }
    }
}
