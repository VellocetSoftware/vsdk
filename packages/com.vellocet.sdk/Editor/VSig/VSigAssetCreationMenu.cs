// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Vellocet.Sdk.Editor.VSig
{
    internal static class VSigAssetCreationMenu
    {
        private const string VSigMenuPath = "Assets/Create/Vellocet/SDK/VSig Script";
        private const string VSigLibraryMenuPath = "Assets/Create/Vellocet/SDK/VSig Library Script";
        private const string DefaultVSigFileName = "New VSig Script.vsig";
        private const string DefaultVSigLibraryFileName = "New VSig Library Script.vsiglib";
        private const string DefaultVSigLibraryContent = "# VSig library script\n";

        [MenuItem(VSigMenuPath, false, 1200)]
        internal static void CreateVSigScript()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Constants.IconsPath}/vsig.svg");
            ProjectWindowUtil.CreateAssetWithContent(DefaultVSigFileName, VSigMapSourceUtility.DefaultSourceContent,
                icon);
        }

        [MenuItem(VSigLibraryMenuPath, false, 1201)]
        internal static void CreateVSigLibraryScript()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Constants.IconsPath}/vsiglib.svg");
            ProjectWindowUtil.CreateAssetWithContent(DefaultVSigLibraryFileName, DefaultVSigLibraryContent, icon);
        }
    }
}
#endif