// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;

namespace Vellocet.Sdk.Utilities
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ReadOnly : PropertyAttribute
    {
    }
}