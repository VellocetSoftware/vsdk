// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.VSig.Runtime
{
    public static class VSigRuntimeRegistry
    {
        private static readonly List<VSigRuntimeContext> Contexts = new();
        private static readonly Dictionary<EntityMarker, IEntitySignalRuntime> RuntimeByMarker = new();
        private static readonly Dictionary<Component, EntityMarker> MarkerByComponent = new();

        public static event Action<VSigSignalEvent> SignalEmitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents()
        {
            ClearAll();
            SignalEmitted = null;
        }

        internal static void PublishSignal(string sourceAlias, string outputName,
            IReadOnlyList<VSigValue> values, int sceneHandle)
        {
            var payload = new object[values?.Count ?? 0];
            for (var i = 0; i < payload.Length; i++)
                payload[i] = values[i].Kind == VSigValueKind.State
                    ? values[i].ToString()
                    : values[i].Value;
            SignalEmitted?.Invoke(new VSigSignalEvent(sourceAlias, outputName, payload, sceneHandle));
        }

        public static void RegisterContext(VSigRuntimeContext context)
        {
            if (context == null || Contexts.Contains(context))
                return;

            foreach (var kv in RuntimeByMarker)
                context.OnRuntimeRegistered(kv.Key, kv.Value);
            Contexts.Add(context);
        }

        public static void UnregisterContext(VSigRuntimeContext context)
        {
            if (context == null)
                return;

            Contexts.Remove(context);
            context.Dispose();
        }

        public static void RegisterRuntime(EntityMarker marker, Component component)
        {
            if (!marker || !component || component is not IEntitySignalRuntime runtime)
                return;
            if (!runtime.SignalHandle.IsEmpty && !runtime.SignalHandle.Equals(EntitySignalHandle.ForEntity(marker)))
                throw new ArgumentException("SDK runtime identity does not match its marker.");

            RuntimeByMarker[marker] = runtime;
            MarkerByComponent[component] = marker;

            for (var i = 0; i < Contexts.Count; i++)
                Contexts[i].OnRuntimeRegistered(marker, runtime);
        }

        public static void UnregisterRuntime(EntityMarker marker)
        {
            if (ReferenceEquals(marker, null))
                return;

            RuntimeByMarker.Remove(marker);

            Component componentToRemove = null;
            foreach (var kv in MarkerByComponent)
                if (kv.Value == marker)
                {
                    componentToRemove = kv.Key;
                    break;
                }

            if (!ReferenceEquals(componentToRemove, null))
                MarkerByComponent.Remove(componentToRemove);

            for (var i = 0; i < Contexts.Count; i++)
                Contexts[i].OnRuntimeUnregistered(marker);
        }

        public static void UnregisterRuntime(Component component)
        {
            if (ReferenceEquals(component, null))
                return;

            if (MarkerByComponent.TryGetValue(component, out var marker))
                UnregisterRuntime(marker);
        }

        public static void ClearAll()
        {
            for (var i = 0; i < Contexts.Count; i++)
                Contexts[i]?.Dispose();
            RuntimeByMarker.Clear();
            MarkerByComponent.Clear();
            Contexts.Clear();
        }

        public static void EmitGlobal(string sourceAlias, string outputName, int sceneHandle = 0,
            params object[] values)
        {
            if (string.IsNullOrWhiteSpace(sourceAlias) || string.IsNullOrWhiteSpace(outputName)) return;
            var payload = new VSigValue[values?.Length ?? 0];
            for (var i = 0; i < payload.Length; i++)
                payload[i] = VSigValue.FromObject(values[i]);

            var dispatched = false;
            for (var i = 0; i < Contexts.Count; i++)
            {
                var context = Contexts[i];
                if (context == null || (sceneHandle != 0 && context.SceneHandle != sceneHandle))
                    continue;
                dispatched = true;
                context.HandleGlobalEmit(sourceAlias, outputName, payload);
            }
            // Native listeners also work on maps with no VSig program. A context already publishes
            // its accepted signals, so never publish a second copy when it handled the emission.
            if (!dispatched) PublishSignal(sourceAlias, EntitySignalNameUtility.Normalize(outputName), payload, sceneHandle);
        }

        public static void Emit(Component sourceRuntime, int outputIndex, string parameter)
        {
            Emit(sourceRuntime, outputIndex, parameter, default);
        }

        public static void Emit(Component sourceRuntime, int outputIndex, string parameter,
            EntitySignalContext signalContext)
        {
            if (!sourceRuntime || !MarkerByComponent.TryGetValue(sourceRuntime, out var sourceMarker) || !sourceMarker)
                return;

            var outputs = sourceMarker.Outputs;
            if (outputs == null || outputIndex < 0 || outputIndex >= outputs.Length)
                return;

            var outputName = outputs[outputIndex];
            Emit(sourceRuntime, outputName, parameter, signalContext);
        }

        public static void Emit(Component sourceRuntime, string outputName, string parameter = null)
        {
            Emit(sourceRuntime, outputName, parameter, default);
        }

        public static void Emit(Component sourceRuntime, string outputName, string parameter,
            EntitySignalContext signalContext)
        {
            if (!sourceRuntime || !MarkerByComponent.TryGetValue(sourceRuntime, out var sourceMarker) ||
                !sourceMarker || string.IsNullOrWhiteSpace(outputName))
                return;

            if (!sourceMarker.SignalContract.TryFindOutput(outputName, out _))
                return;

            if (signalContext.IsEmpty && sourceRuntime is IEntitySignalContextSource contextSource)
                signalContext = contextSource.CurrentSignalContext;
            signalContext = signalContext.WithSelf(EntitySignalHandle.ForEntity(sourceMarker));

            for (var i = 0; i < Contexts.Count; i++)
                Contexts[i].HandleEmit(sourceMarker, outputName, parameter, signalContext);
        }
    }

    public readonly struct VSigSignalEvent
    {
        public VSigSignalEvent(string sourceAlias, string outputName, object[] values, int sceneHandle)
        {
            SourceAlias = sourceAlias ?? string.Empty;
            OutputName = outputName ?? string.Empty;
            Values = values ?? Array.Empty<object>();
            SceneHandle = sceneHandle;
        }

        public string SourceAlias { get; }
        public string OutputName { get; }
        public object[] Values { get; }
        public int SceneHandle { get; }
    }
}
