// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Editor.Entities
{
    internal static class EntitySignalContractFormatter
    {
        public static string[] Names(IReadOnlyList<EntitySignalMember> members)
        {
            if (members == null || members.Count == 0)
                return Array.Empty<string>();

            var values = new List<string>(members.Count);
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member != null && !string.IsNullOrWhiteSpace(member.Name))
                    values.Add(member.Name);
            }

            return values.ToArray();
        }

        public static string[] Rows(IReadOnlyList<EntitySignalMember> members)
        {
            if (members == null || members.Count == 0)
                return Array.Empty<string>();

            var values = new List<string>(members.Count);
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || string.IsNullOrWhiteSpace(member.Name))
                    continue;

                values.Add($"{member.Name}: {TypeName(member)} {Access(member)}");
            }

            return values.ToArray();
        }

        private static string TypeName(EntitySignalMember member)
        {
            return string.IsNullOrWhiteSpace(member.TypeName)
                ? member.ValueType.ToString().ToLowerInvariant()
                : member.TypeName;
        }

        private static string Access(EntitySignalMember member)
        {
            if (member.Readable && member.Writable)
                return "read/write";
            if (member.Readable)
                return "read";
            return member.Writable ? "write" : "internal";
        }
    }
}
#endif