// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities.Base
{
    /// <summary>
    ///     Builds a stable name array for an enum T where indices are the enum's underlying int values.
    ///     Uses [InspectorName] if present, else falls back to the member name.
    ///     Constructed once per T.
    /// </summary>
    public static class SignalNames<T> where T : struct, Enum
    {
        public static readonly string[] Array = Build();

        private static string[] Build()
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var max = -1;
            foreach (var t in values)
                max = Math.Max(max, Convert.ToInt32(t));

            var result = new string[Math.Max(0, max + 1)];
            foreach (var v in values)
            {
                var idx = Convert.ToInt32(v);
                var name = v.ToString();

                var members = typeof(T).GetMember(name, BindingFlags.Public | BindingFlags.Static);
                var label = name;

                if (members.Length > 0)
                {
                    var insp = members[0].GetCustomAttribute<InspectorNameAttribute>();
                    if (insp != null && !string.IsNullOrEmpty(insp.displayName))
                    {
                        label = insp.displayName;
                    }
                    else
                    {
                        var desc = members[0].GetCustomAttribute<DescriptionAttribute>();
                        if (desc != null && !string.IsNullOrEmpty(desc.Description))
                            label = desc.Description;
                    }
                }

                if (idx >= 0 && idx < result.Length)
                    result[idx] = EntitySignalNameUtility.Normalize(label);
            }

            return result;
        }
    }
}