using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("env_aniv_audio_occlusion")]
    [EntityAuthoring("Audio", EntityPlacementKind.Logic, "Audio Occlusion")]
    [RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
    public sealed class AudioOcclusionMarker : EntityMarker, IEntityImportGroup
    {
        [SerializeField] private EntityMarker[] probes = Array.Empty<EntityMarker>();
        [SerializeField] private bool overrideKeepPriority;
        public IReadOnlyList<EntityMarker> Probes => probes;
        public bool OverrideKeepPriority => overrideKeepPriority;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
