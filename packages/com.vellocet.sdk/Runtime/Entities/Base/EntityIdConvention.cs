// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;

namespace Vellocet.Sdk.Entities.Base
{
    /// <summary>
    ///     Canonical entity IDs use a semantic family prefix followed by lowercase snake_case tokens.
    /// </summary>
    public static class EntityIdConvention
    {
        public const string Description =
            "lowercase snake_case beginning with func_, trigger_, info_, env_, logic_, item_, prop_, group_, or game_";

        public static bool IsCanonical(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId) ||
                !string.Equals(entityId, entityId.Trim(), StringComparison.Ordinal))
                return false;

            var separator = entityId.IndexOf('_');
            if (separator <= 0 || separator == entityId.Length - 1 || !HasKnownPrefix(entityId, separator))
                return false;

            var tokenStart = separator + 1;
            for (var i = tokenStart; i < entityId.Length; i++)
            {
                var character = entityId[i];
                if (character == '_')
                {
                    if (i == tokenStart)
                        return false;

                    tokenStart = i + 1;
                    continue;
                }

                if (i == tokenStart && !IsLowerAsciiLetter(character))
                    return false;
                if (!IsLowerAsciiLetter(character) && character is not (>= '0' and <= '9'))
                    return false;
            }

            return tokenStart < entityId.Length;
        }

        public static string GetIconPath(string entityId)
        {
            return IsCanonical(entityId) ? $"{Constants.EntityIconsPath}/{entityId}.png" : null;
        }

        private static bool HasKnownPrefix(string entityId, int separator)
        {
            var prefix = entityId.Substring(0, separator);
            return prefix is "func" or "trigger" or "info" or "env" or "logic" or "item" or "prop" or "group" or "game";
        }

        private static bool IsLowerAsciiLetter(char character)
        {
            return character is >= 'a' and <= 'z';
        }
    }
}
