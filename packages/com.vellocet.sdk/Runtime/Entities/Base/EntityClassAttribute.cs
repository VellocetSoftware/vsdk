// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine.Scripting;

namespace Vellocet.Sdk.Entities.Base
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    [Preserve]
    public sealed class EntityClassAttribute : Attribute
    {
        public EntityClassAttribute(string className)
        {
            ClassName = className?.Trim();
        }

        public string ClassName { get; }
    }
}