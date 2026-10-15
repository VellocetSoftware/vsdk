using System;
using System.IO;
using UnityEngine;
using Vellocet.Framework.Runtime.Audio.Environment;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("trigger_audio_environment")]
    [EntityAuthoring("Audio", EntityPlacementKind.Volume, "Audio Environment")]
    [DisallowMultipleComponent]
    public sealed class AudioEnvironmentMarker : TriggerVolumeMarkerBase<AudioEnvironmentMarker.Input, AudioEnvironmentMarker.Output>
    {
        public enum Input { }
        public enum Output { }
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        [SerializeField] private bool overrideRoomSize = true;
        [SerializeField, Range(0, 1)] private float roomSize = .5f;
        [SerializeField] private int priority;
        [SerializeField, Min(0)] private float transitionSeconds = 1f;
        [SerializeField] private AudioEnvironmentParameter[] parameters = Array.Empty<AudioEnvironmentParameter>();
        protected override Color GizmoFillColor => new(.2f, .6f, 1f, .12f);
        protected override Color GizmoWireColor => new(.2f, .6f, 1f, .9f);
        public void ValidateEnvironment()
        {
            if (!float.IsFinite(roomSize) || roomSize < 0 || roomSize > 1 ||
                !float.IsFinite(transitionSeconds) || transitionSeconds < 0)
                throw new InvalidDataException("Audio environment room size or transition is invalid: " + name);
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters ?? Array.Empty<AudioEnvironmentParameter>())
                if (string.IsNullOrWhiteSpace(parameter.Name) || !float.IsFinite(parameter.Value) ||
                    parameter.Name == AudioEnvironmentResolver.RoomSizeParameter || !seen.Add(parameter.Name))
                    throw new InvalidDataException("Audio environment has an invalid, duplicate, or reserved parameter: " + parameter.Name);
        }
        public AudioEnvironmentZone BindRuntime()
        {
            ValidateEnvironment();
            var zone = GetComponent<AudioEnvironmentZone>();
            if (!zone) zone = gameObject.AddComponent<AudioEnvironmentZone>();
            zone.StableId = Guid.ToString("N"); zone.Owner = this;
            zone.OverrideRoomSize = overrideRoomSize; zone.RoomSize = roomSize;
            zone.Priority = priority; zone.TransitionSeconds = transitionSeconds;
            zone.Parameters = parameters; zone.Center = new Vector3(0, .5f, 0); zone.Size = Vector3.one;
            return zone;
        }
    }
}
