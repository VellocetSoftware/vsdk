// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Vellocet.Sdk.Utilities
{
    /// <summary>
    ///     Reads and validates the stable asset identifier stored in a Unity .meta file.
    /// </summary>
    public static class SdkUnityMetaFileUtility
    {
        private const string GuidPrefix = "guid:";
        private const int GuidLength = 32;

        private static readonly Regex GuidLinePattern = new(
            "^guid:[ \\t]*([0-9A-Fa-f]{32})[ \\t]*\\r?$",
            RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        private static readonly UTF8Encoding StrictUtf8 = new(
            false,
            true
        );

        public static bool TryReadGuid(string absoluteMetaPath, out string guid)
        {
            guid = string.Empty;
            if (string.IsNullOrWhiteSpace(absoluteMetaPath) || !File.Exists(absoluteMetaPath))
                return false;

            try
            {
                return TryParseGuid(File.ReadLines(absoluteMetaPath), out guid);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryParseGuid(IEnumerable<string> lines, out string guid)
        {
            guid = string.Empty;
            if (lines == null)
                return false;

            foreach (var line in lines)
            {
                if (
                    string.IsNullOrWhiteSpace(line)
                    || !line.StartsWith(GuidPrefix, StringComparison.Ordinal)
                )
                    continue;

                var candidate = line[GuidPrefix.Length..].Trim();
                if (!IsGuid(candidate))
                    return false;

                guid = candidate.ToLowerInvariant();
                return true;
            }

            return false;
        }

        public static bool TryReadGuid(byte[] contents, out string guid)
        {
            guid = string.Empty;
            if (contents == null || contents.Length == 0)
                return false;

            try
            {
                var matches = GuidLinePattern.Matches(StrictUtf8.GetString(contents));
                if (matches.Count != 1)
                    return false;

                guid = matches[0].Groups[1].Value.ToLowerInvariant();
                return IsGuid(guid);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryReplaceGuid(
            byte[] contents,
            string replacementGuid,
            out byte[] replacedContents,
            out string originalGuid
        )
        {
            replacedContents = Array.Empty<byte>();
            originalGuid = string.Empty;
            if (contents == null || !IsGuid(replacementGuid))
                return false;

            try
            {
                var text = StrictUtf8.GetString(contents);
                var matches = GuidLinePattern.Matches(text);
                if (matches.Count != 1)
                    return false;

                var guidGroup = matches[0].Groups[1];
                originalGuid = guidGroup.Value.ToLowerInvariant();
                var replacedText = text.Remove(guidGroup.Index, guidGroup.Length)
                    .Insert(guidGroup.Index, replacementGuid.ToLowerInvariant());
                replacedContents = StrictUtf8.GetBytes(replacedText);
                return replacedContents.Length == contents.Length;
            }
            catch
            {
                replacedContents = Array.Empty<byte>();
                originalGuid = string.Empty;
                return false;
            }
        }

        public static bool IsGuid(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != GuidLength)
                return false;

            foreach (var character in value)
            {
                var isDigit = character is >= '0' and <= '9';
                var isLowerHex = character is >= 'a' and <= 'f';
                var isUpperHex = character is >= 'A' and <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                    return false;
            }

            return true;
        }
    }
}