// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

namespace Vellocet.Sdk
{
    /// <summary>Canonical identity rules shared by the SDK, game, server, and addon tools.</summary>
    public static class AddonIdPolicy
    {
        public const int MaximumLength = 96;

        public static bool TryNormalize(string value, out string normalized)
        {
            return DottedIdPolicy.TryNormalize(value, MaximumLength, out normalized);
        }

        public static string NormalizeOrEmpty(string value)
        {
            return TryNormalize(value, out var normalized) ? normalized : string.Empty;
        }
    }

    /// <summary>
    ///     Stable game-mode identity. Dots are meaningful and are never rewritten or removed.
    /// </summary>
    public static class ModeIdPolicy
    {
        public const int MaximumLength = 96;
        public static string SafeFallbackModeId => SdkGame.Current.FallbackModeId;

        public static bool TryNormalize(string value, out string normalized)
        {
            return DottedIdPolicy.TryNormalize(value, MaximumLength, out normalized);
        }

        public static string NormalizeOrEmpty(string value)
        {
            return TryNormalize(value, out var normalized) ? normalized : string.Empty;
        }
    }

    /// <summary>Short map-prefix convention, such as koth, as, or ze.</summary>
    public static class MapPrefixPolicy
    {
        public const int MaximumLength = 16;

        public static bool TryNormalize(string value, out string normalized)
        {
            normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length is < 1 or > MaximumLength || normalized[0] is < 'a' or > 'z')
            {
                normalized = string.Empty;
                return false;
            }

            for (var i = 1; i < normalized.Length; i++)
            {
                var c = normalized[i];
                if (c is >= 'a' and <= 'z' || c is >= '0' and <= '9')
                    continue;
                normalized = string.Empty;
                return false;
            }

            return true;
        }

        public static string NormalizeOrEmpty(string value)
        {
            return TryNormalize(value, out var normalized) ? normalized : string.Empty;
        }

        public static bool TryExtractFromMapId(string mapId, out string prefix)
        {
            prefix = string.Empty;
            var normalizedMapId = ContentIdUtility.Sanitize(mapId);
            if (string.IsNullOrWhiteSpace(normalizedMapId))
                return false;

            var separator = normalizedMapId.IndexOf('_');
            var token = separator > 0 ? normalizedMapId[..separator] : normalizedMapId;
            return TryNormalize(token, out prefix);
        }
    }

    internal static class DottedIdPolicy
    {
        public static bool TryNormalize(string value, int maximumLength, out string normalized)
        {
            normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length is < 1 || normalized.Length > maximumLength ||
                !IsAlphaNumeric(normalized[0]) || !IsAlphaNumeric(normalized[^1]))
            {
                normalized = string.Empty;
                return false;
            }

            for (var i = 0; i < normalized.Length; i++)
            {
                var c = normalized[i];
                if (IsAlphaNumeric(c) || c is '_' or '-')
                    continue;

                if (c == '.' && i > 0 && i + 1 < normalized.Length &&
                    IsAlphaNumeric(normalized[i - 1]) && IsAlphaNumeric(normalized[i + 1]))
                    continue;

                normalized = string.Empty;
                return false;
            }

            return true;
        }

        private static bool IsAlphaNumeric(char c)
        {
            return c is >= 'a' and <= 'z' || c is >= '0' and <= '9';
        }
    }
}