// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Text;

namespace Vellocet.Sdk
{
    public static class ContentIdUtility
    {
        public static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var input = value.Trim();
            var builder = new StringBuilder(input.Length);
            var lastWasSeparator = false;

            foreach (var c in input)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                    lastWasSeparator = false;
                    continue;
                }

                if (c != '_' && c != '-' && !char.IsWhiteSpace(c))
                    continue;

                if (builder.Length == 0 || lastWasSeparator)
                    continue;

                builder.Append('_');
                lastWasSeparator = true;
            }

            while (builder.Length > 0 && builder[^1] == '_')
                builder.Length--;

            return builder.ToString();
        }
    }
}