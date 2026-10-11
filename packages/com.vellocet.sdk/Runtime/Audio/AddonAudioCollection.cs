// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;

namespace Vellocet.Sdk.Audio
{
    /// <summary>
    ///     Authoring set for exporting ordinary audio files into an existing map or server-mod addon package.
    ///     The game decodes those files and sends PCM through host-authored Wwise Audio Input events at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "AddonAudioCollection", menuName = "Vellocet/SDK/Audio/Audio Collection",
        order = 121)]
    public sealed class AddonAudioCollection : ScriptableObject
    {
        [SerializeField] private string addonId = string.Empty;
        [SerializeField] private string contentId = string.Empty;
        [SerializeField] private string title = "Custom Audio";
        [SerializeField] private AddonAudioCue[] cues = Array.Empty<AddonAudioCue>();

        public string AddonId => addonId?.Trim() ?? string.Empty;
        public string ContentId => ResolveContentId(AddonId);
        public string Title => title?.Trim() ?? string.Empty;
        public AddonAudioCue[] Cues => cues ?? Array.Empty<AddonAudioCue>();

        public string ResolveContentId(string ownerAddonId)
        {
            return string.IsNullOrWhiteSpace(contentId)
                ? ContentIdUtility.Sanitize(AddonIdPolicy.NormalizeOrEmpty(ownerAddonId) + "_audio")
                : ContentIdUtility.Sanitize(contentId);
        }
    }
}