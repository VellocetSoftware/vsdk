// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;

namespace Vellocet.Sdk
{
    public readonly struct ModeDescriptor
    {
        public ModeDescriptor(string id, string displayName, string localizationKey, string mapIdPrefix,
            params string[] aliases)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            LocalizationKey = localizationKey ?? string.Empty;
            MapIdPrefix = string.IsNullOrWhiteSpace(mapIdPrefix) ? Id : mapIdPrefix;
            Aliases = aliases ?? Array.Empty<string>();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string LocalizationKey { get; }
        public string MapIdPrefix { get; }
        public IReadOnlyList<string> Aliases { get; }
    }

    public static partial class ModeCatalog
    {


        private static IReadOnlyList<ModeDescriptor> Modes => SdkGame.Current.Modes;
        private static SdkGameDefinition _registeredGame;

        private static readonly Dictionary<string, ModeDescriptor> ModesByCanonicalId =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> CanonicalIdByToken =
            new(StringComparer.OrdinalIgnoreCase);

        private static void EnsureGame()
        {
            if (ReferenceEquals(_registeredGame, SdkGame.Current)) return;
            _registeredGame = SdkGame.Current;
            ModesByCanonicalId.Clear();
            CanonicalIdByToken.Clear();
            foreach (var mode in Modes)
            {
                if (string.IsNullOrWhiteSpace(mode.Id))
                    continue;

                ModesByCanonicalId[mode.Id] = mode;
                RegisterToken(mode.Id, mode.Id);
                RegisterToken(mode.MapIdPrefix, mode.Id);

                var aliases = mode.Aliases;
                if (aliases == null)
                    continue;

                foreach (var alias in aliases)
                    RegisterToken(alias, mode.Id);
            }
        }

        public static string DefaultModeId => SdkGame.Current.DefaultModeId;
        public static IReadOnlyList<ModeDescriptor> All => Modes;

        public static bool TryGetByCanonicalId(string modeId, out ModeDescriptor descriptor)
        {
            descriptor = default;
            return TryResolveId(modeId, out var canonical) &&
                   ModesByCanonicalId.TryGetValue(canonical, out descriptor);
        }

        public static string NormalizeId(string modeId, string fallbackModeId = null)
        {
            if (TryResolveId(modeId, out var resolved))
                return resolved;

            if (ModeIdPolicy.TryNormalize(modeId, out resolved))
                return resolved;

            if (TryResolveId(fallbackModeId, out resolved))
                return resolved;

            if (ModeIdPolicy.TryNormalize(fallbackModeId, out resolved))
                return resolved;

            return DefaultModeId;
        }

        public static bool TryResolveId(string token, out string canonicalModeId)
        {
            EnsureGame();
            canonicalModeId = string.Empty;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var normalized = ContentIdUtility.Sanitize(token);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            if (CanonicalIdByToken.TryGetValue(normalized, out canonicalModeId))
                return true;

            return false;
        }

        public static string ResolveMapIdPrefix(string modeId, string fallbackModeId = null)
        {
            var canonicalModeId = NormalizeId(modeId, fallbackModeId);
            return TryGetByCanonicalId(canonicalModeId, out var descriptor) &&
                   !string.IsNullOrWhiteSpace(descriptor.MapIdPrefix)
                ? descriptor.MapIdPrefix
                : canonicalModeId;
        }

        public static string ResolveDisplayName(string modeId, string fallback = "Unknown")
        {
            if (TryGetByCanonicalId(modeId, out var descriptor) &&
                !string.IsNullOrWhiteSpace(descriptor.DisplayName))
                return descriptor.DisplayName;

            if (string.IsNullOrWhiteSpace(modeId))
                return fallback;

            var normalized = ModeIdPolicy.NormalizeOrEmpty(modeId);
            if (string.IsNullOrWhiteSpace(normalized))
                return fallback;

            return normalized.Replace('_', ' ').Replace('.', ' ');
        }

        private static void RegisterToken(string token, string canonicalId)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(canonicalId))
                return;

            var normalizedToken = ContentIdUtility.Sanitize(token);
            var normalizedId = ContentIdUtility.Sanitize(canonicalId);
            if (string.IsNullOrWhiteSpace(normalizedToken) || string.IsNullOrWhiteSpace(normalizedId))
                return;

            CanonicalIdByToken[normalizedToken] = normalizedId;
        }
    }
}
