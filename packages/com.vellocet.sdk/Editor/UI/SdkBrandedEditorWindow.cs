// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Vellocet.Sdk.Editor
{
    public abstract class SdkBrandedEditorWindow : EditorWindow
    {
        private Texture2D _sdkLogo;

        protected void DrawSdkHeader(string title, string eyebrow, string description, float height = 104f)
        {
            if (!_sdkLogo)
                ReloadSdkBrandingAssets();

            SdkEditorGui.DrawHero(_sdkLogo, title, eyebrow, description, height);
        }

        protected void ReloadSdkBrandingAssets()
        {
            _sdkLogo = AssetDatabase.LoadAssetAtPath<Texture2D>(Constants.LogoPath);
        }
    }
}
#endif