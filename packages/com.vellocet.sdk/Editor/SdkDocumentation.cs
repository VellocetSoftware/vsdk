// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Vellocet.Sdk.Editor
{
    internal enum SdkDocumentationPage
    {
        Home,
        GettingStarted,
        ContentAddons,
        Audio,
        ReusableContent,
        VSig,
        Maps,
        BuildAndTest,
        PackageReference,
        Troubleshooting
    }

    internal static class SdkDocumentation
    {
        [MenuItem("Tools/Vellocet/SDK/Documentation", priority = 121)]
        internal static void OpenHome()
        {
            Open(SdkDocumentationPage.Home);
        }

        [MenuItem("Tools/Vellocet/SDK/Documentation/Getting Started", priority = 122)]
        private static void OpenGettingStarted()
        {
            Open(SdkDocumentationPage.GettingStarted);
        }

        [MenuItem("Tools/Vellocet/SDK/Documentation/Content Add-ons", priority = 123)]
        private static void OpenContentAddons()
        {
            Open(SdkDocumentationPage.ContentAddons);
        }

        [MenuItem("Tools/Vellocet/SDK/Documentation/Audio", priority = 124)]
        private static void OpenAudio()
        {
            Open(SdkDocumentationPage.Audio);
        }

        [MenuItem("Tools/Vellocet/SDK/Documentation/Reusable Content Browser", priority = 125)]
        private static void OpenReusableContent()
        {
            Open(SdkDocumentationPage.ReusableContent);
        }

        [MenuItem("Tools/Vellocet/SDK/Documentation/VSig", priority = 127)]
        private static void OpenVSig()
        {
            Open(SdkDocumentationPage.VSig);
        }

        [MenuItem("Tools/Vellocet/SDK/Documentation/Maps", priority = 128)]
        private static void OpenMaps()
        {
            Open(SdkDocumentationPage.Maps);
        }

        [MenuItem("Tools/Vellocet/SDK/Documentation/Build and Test", priority = 130)]
        private static void OpenBuildAndTest()
        {
            Open(SdkDocumentationPage.BuildAndTest);
        }

        [MenuItem("Tools/Vellocet/SDK/Documentation/Package Reference", priority = 131)]
        private static void OpenPackageReference()
        {
            Open(SdkDocumentationPage.PackageReference);
        }

        [MenuItem("Tools/Vellocet/SDK/Documentation/Troubleshooting", priority = 132)]
        private static void OpenTroubleshooting()
        {
            Open(SdkDocumentationPage.Troubleshooting);
        }

        internal static void Open(SdkDocumentationPage page)
        {
            Application.OpenURL(GetUrl(page));
        }

        internal static string GetUrl(SdkDocumentationPage page)
        {
            const string wikiRoot = "https://developer.vellocetsoftware.com/wiki/";
            var section = SdkGame.Current.DocumentationSection;
            if (string.IsNullOrEmpty(section)) return Constants.OfficialDocumentationUrl;
            return page switch
            {
                SdkDocumentationPage.GettingStarted => wikiRoot + section + "/Getting_started",
                SdkDocumentationPage.ContentAddons => wikiRoot + section + "/Addons#Build_packages_with_the_SDK",
                SdkDocumentationPage.Audio => wikiRoot + "Vellocet_SDK#Custom_audio",
                SdkDocumentationPage.ReusableContent => wikiRoot + section + "/Addons#Browse_reusable_content",
                SdkDocumentationPage.VSig => wikiRoot + section + "/VSig",
                SdkDocumentationPage.Maps => wikiRoot + section + "/Mapping",
                SdkDocumentationPage.BuildAndTest =>
                    wikiRoot + section + "/Steam_Workshop_addons#Prepare_a_Release_package",
                SdkDocumentationPage.PackageReference => wikiRoot + section + "/Addons#Package_layout",
                SdkDocumentationPage.Troubleshooting =>
                    wikiRoot + section + "/Addons#Fix_a_package_that_does_not_load",
                _ => Constants.OfficialDocumentationUrl
            };
        }
    }
}
#endif