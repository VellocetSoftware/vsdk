using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Stereoform;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Entities
{
    internal static partial class VertexEntityProperties
    {
        private static IEnumerable<EntityPropertyDefinition> AudioProperties(EntityMarker marker)
        {
            if (marker is not IEntityImportGroup) yield break;
            yield return Field("audio_emitter", "Audio emitter", "bool", false, group: "Audio");
            var visible = new[] { new EntityWarningCondition { Property = "audio_emitter", Values = new object[] { true } } };
            yield return Field("audio_event", "Audio event", "asset", null, reference: "audio", group: "Audio", visibleWhen: visible);
            yield return Field("audio_bindings", "Audio bindings", "asset", null, reference: "data", group: "Audio", visibleWhen: visible);
            yield return Field("audio_auto_play", "Play on start", "bool", true, group: "Audio", visibleWhen: visible);
            yield return Field("audio_loop", "Loop", "bool", false, group: "Audio", visibleWhen: visible);
            yield return Field("audio_occlusion", "Spatial occlusion", "bool", true, group: "Audio", visibleWhen: visible);
            yield return Field("audio_muted", "Muted", "bool", false, group: "Audio", visibleWhen: visible);
            yield return Field("audio_enabled", "Emitter enabled", "bool", true, group: "Audio", visibleWhen: visible);
        }

        private static bool IsAudioProperty(EntityMarker marker, string id) => marker is IEntityImportGroup &&
            id is "audio_emitter" or "audio_event" or "audio_bindings" or "audio_auto_play" or "audio_loop" or "audio_occlusion" or "audio_muted" or "audio_enabled";

        internal static void RestoreAudioEmitter(EntityMarker marker, MapContainer source, VertexEntityBuildContext context)
        {
            if (marker is not IEntityImportGroup) return;
            var values = source.Entity.Properties.ToDictionary(p => p.Id, p => p.Value);
            if (!values.TryGetValue("audio_emitter", out var enabled) || !(bool)enabled) return;
            var emitter = marker.GetComponent<AudioEmitter>();
            if (!emitter) emitter = marker.gameObject.AddComponent<AudioEmitter>();
            emitter.Event = (AudioEventReference)ResolveAsset(typeof(AudioEventReference), Value("audio_event", null), context);
            emitter.Bindings = (AudioEventBindings)ResolveAsset(typeof(AudioEventBindings), Value("audio_bindings", null), context);
            emitter.AutoPlay = (bool)Value("audio_auto_play", true);
            emitter.Loop = (bool)Value("audio_loop", false);
            emitter.SpatialOcclusion = (bool)Value("audio_occlusion", true);
            emitter.Muted = (bool)Value("audio_muted", false);
            emitter.enabled = (bool)Value("audio_enabled", true);
            object Value(string id, object fallback) => values.TryGetValue(id, out var value) ? value : fallback;
        }
    }
}
