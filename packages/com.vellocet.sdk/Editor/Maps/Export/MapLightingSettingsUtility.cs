// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vellocet.Sdk.Editor.Maps
{
    public enum MapLightmapQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public static class MapLightingSettingsUtility
    {
        private const string RootFolder = Constants.PackageAssetsPath + "/Settings/Lighting";
        private const string LowAssetPath = RootFolder + "/MapBakeLow.lighting";
        private const string MediumAssetPath = RootFolder + "/MapBakeMedium.lighting";
        private const string HighAssetPath = RootFolder + "/MapBakeHigh.lighting";

        public static string GetAssetPath(MapLightmapQuality quality)
        {
            switch (quality)
            {
                case MapLightmapQuality.Low:
                    return LowAssetPath;
                case MapLightmapQuality.High:
                    return HighAssetPath;
                default:
                    return MediumAssetPath;
            }
        }

        public static string GetDisplayName(MapLightmapQuality quality)
        {
            switch (quality)
            {
                case MapLightmapQuality.Low:
                    return "Low";
                case MapLightmapQuality.High:
                    return "High";
                default:
                    return "Medium";
            }
        }

        public static bool TryResolve(MapLightmapQuality quality, bool createIfMissing, out LightingSettings settings,
            out string assetPath, out string error)
        {
            assetPath = GetAssetPath(quality);
            error = null;
            settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(assetPath);
            if (settings)
                return true;

            if (!createIfMissing)
            {
                error = $"Lighting settings asset is missing for {GetDisplayName(quality)} quality.";
                return false;
            }

            try
            {
                EnsureFolder(RootFolder);
                settings = new LightingSettings
                {
                    name = Path.GetFileNameWithoutExtension(assetPath),
                    bakedGI = true,
                    realtimeGI = false,
                    lightmapPadding = 2
                };

                Configure(settings, quality);
                AssetDatabase.CreateAsset(settings, assetPath);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath);
                settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(assetPath);
                if (settings)
                    return true;

                error = $"Lighting settings asset could not be created at '{assetPath}'.";
                return false;
            }
            catch (Exception ex)
            {
                settings = null;
                error = ex.Message;
                return false;
            }
        }

        internal static bool Apply(MapLightmapQuality quality, out string assetPath, out string message)
        {
            assetPath = GetAssetPath(quality);
            if (!TryResolve(quality, true, out var settings, out assetPath, out var error) || !settings)
            {
                message = string.IsNullOrWhiteSpace(error)
                    ? $"Lighting settings for {GetDisplayName(quality)} could not be resolved."
                    : error;
                return false;
            }

            Lightmapping.lightingSettings = settings;
            message = $"Applied {GetDisplayName(quality)} lightmap quality using '{assetPath}'.";
            return true;
        }

        private static void Configure(LightingSettings settings, MapLightmapQuality quality)
        {
            if (!settings)
                return;

            switch (quality)
            {
                case MapLightmapQuality.Low:
                    settings.lightmapResolution = 16f;
                    settings.indirectResolution = 1f;
                    settings.directSampleCount = 16;
                    settings.indirectSampleCount = 64;
                    settings.environmentSampleCount = 32;
                    settings.lightProbeSampleCountMultiplier = 2f;
                    settings.maxBounces = 2;
                    settings.lightmapMaxSize = 1024;
                    break;
                case MapLightmapQuality.High:
                    settings.lightmapResolution = 36f;
                    settings.indirectResolution = 2f;
                    settings.directSampleCount = 64;
                    settings.indirectSampleCount = 512;
                    settings.environmentSampleCount = 128;
                    settings.lightProbeSampleCountMultiplier = 6f;
                    settings.maxBounces = 4;
                    settings.lightmapMaxSize = 2048;
                    break;
                default:
                    settings.lightmapResolution = 24f;
                    settings.indirectResolution = 1.5f;
                    settings.directSampleCount = 32;
                    settings.indirectSampleCount = 256;
                    settings.environmentSampleCount = 64;
                    settings.lightProbeSampleCountMultiplier = 4f;
                    settings.maxBounces = 2;
                    settings.lightmapMaxSize = 2048;
                    break;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var normalized = folderPath.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            var absolutePath = Path.GetFullPath(normalized);
            if (!Directory.Exists(absolutePath))
                Directory.CreateDirectory(absolutePath);

            AssetDatabase.Refresh();
        }
    }
}
#endif