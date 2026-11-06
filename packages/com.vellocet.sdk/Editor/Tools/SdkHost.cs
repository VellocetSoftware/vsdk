// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
namespace Vellocet.Sdk.Editor.Tools
{
    public static class SdkHost
    {
        public static SdkEditorActions Actions { get; set; }
        public static bool IsBaseProject => Actions?.BaseMaps != null;
    }
}
#endif
