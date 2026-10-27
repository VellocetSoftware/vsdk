// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;

namespace Vellocet.Sdk.Audio
{
    /// <summary>
    ///     Stable reference placed in maps. Embedded cues retain an editor-only source GUID for export. Dependency
    ///     cues retain only the provider addon id and cue id, so shared media is never copied into the map package.
    /// </summary>
    [CreateAssetMenu(fileName = "AddonAudioCue", menuName = "Vellocet/SDK/Audio/Audio Cue", order = 120)]
#if VELLOCET_STEREOFORM
    public sealed class AddonAudioCue : Vellocet.Stereoform.AudioEventReference
#else
    public sealed class AddonAudioCue : ScriptableObject
#endif
    {
        [SerializeField] private AddonAudioCueOrigin origin = AddonAudioCueOrigin.Embedded;
        [SerializeField] private string cueId = string.Empty;
        [SerializeField] private AddonAudioTemplate template = AddonAudioTemplate.Sfx3D;
        [SerializeField] [Range(0f, 1f)] private float gain = 1f;
        [SerializeField] private string dependencyAddonId = string.Empty;
        [SerializeField] [HideInInspector] private string sourceAssetGuid = string.Empty;
        [SerializeField] [HideInInspector] private byte[] sourceMedia;
        [SerializeField] [HideInInspector] private string mediaExtension;

#if VELLOCET_STEREOFORM
        public override string Bank => AddonAudioBanks.TryResolve(CueId, out var entry) ? entry.Bank : base.Bank;
        public override string Id => AddonAudioBanks.TryResolve(CueId, out var entry) ? entry.Event : base.Id;
        public override float Duration => AddonAudioBanks.TryResolve(CueId, out var entry) ? entry.Duration : base.Duration;
#endif
        public AddonAudioCueOrigin Origin => origin;
        public string CueId => cueId?.Trim() ?? string.Empty;
        public AddonAudioTemplate Template => template;
        public float Gain => Mathf.Clamp01(float.IsFinite(gain) ? gain : 1f);
        public string DependencyAddonId => AddonIdPolicy.NormalizeOrEmpty(dependencyAddonId);
        public string SourceAssetGuid => sourceAssetGuid?.Trim() ?? string.Empty;
        public bool IsDependencyReference => origin == AddonAudioCueOrigin.AddonDependency;
        public byte[] SourceMedia => sourceMedia;
        public string MediaExtension => mediaExtension;
        public void ClearEmbeddedMedia() => sourceMedia = null;
        public void Configure(string id, AddonAudioTemplate playback, float volume, string provider, string sourceGuid, byte[] media, string extension)
        {
            cueId = id; template = playback; gain = volume; dependencyAddonId = provider ?? "";
            origin = string.IsNullOrEmpty(provider) ? AddonAudioCueOrigin.Embedded : AddonAudioCueOrigin.AddonDependency;
            sourceAssetGuid = sourceGuid ?? ""; sourceMedia = media; mediaExtension = extension;
        }
    }
}
