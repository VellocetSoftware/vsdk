// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Linq;

namespace Vellocet.Sdk
{
    public static class Constants
    {
        public const string PackageId = "com.vellocet.sdk";

        public const string OfficialDocumentationUrl =
            "https://developer.vellocetsoftware.com/wiki/Vellocet_SDK";

        public const string RequiredUnityVersion = "6000.3.17f1";
        public const string RootPath = "Packages/" + PackageId;
        public const string PackageAssetsPath = RootPath + "/Assets";
        public const string IconsPath = RootPath + "/Editor/Icons";
        public const string FontsPath = RootPath + "/Editor/Fonts";
        public const string EntityIconsPath = RootPath + "/Editor/Icons/Entities";
        public const string LogoPath = IconsPath + "/logo.png";
        public const string MondaRegularFontPath = FontsPath + "/Monda-Regular.ttf";
        public const string MondaSemiBoldFontPath = FontsPath + "/Monda-SemiBold.ttf";
        public const string MondaBoldFontPath = FontsPath + "/Monda-Bold.ttf";

        public const string SharedMaterialsEntitiesRoot = "Assets/Art/Materials/Entities";
        public const string SharedMaterialsEnvironmentRoot = "Assets/Art/Materials/Environment";
        public const string SharedMaterialsPropsRoot = "Assets/Art/Materials/Props";
        public const string SharedModelsEntitiesRoot = "Assets/Art/Models/Entities";
        public const string SharedModelsModularRoot = "Assets/Art/Models/Modular";
        public const string SharedModelsPropsRoot = "Assets/Art/Models/Props";
        public const string SharedTexturesEntitiesRoot = "Assets/Art/Textures/Entities";
        public const string SharedTexturesEnvironmentRoot = "Assets/Art/Textures/Environment";
        public const string SharedTexturesPropsRoot = "Assets/Art/Textures/Props";
        public const string SharedShadersSkyRoot = "Assets/Art/Shaders/Environment/Sky";
        public const string SharedEntityKitsRoot = "Assets/GameContent/Prefabs/Entities/SDK/Kits";
        public const string SdkManagedContentRoot = "Assets/VellocetSDK";
        public const string SdkMaterialsEntitiesRoot = SdkManagedContentRoot + "/Materials/Entities";
        public const string SdkMaterialsEnvironmentRoot = SdkManagedContentRoot + "/Materials/Environment";
        public const string SdkMaterialsPropsRoot = SdkManagedContentRoot + "/Materials/Props";
        public const string SdkModelsEntitiesRoot = SdkManagedContentRoot + "/Models/Entities";
        public const string SdkModelsModularRoot = SdkManagedContentRoot + "/Models/Modular";
        public const string SdkModelsPropsRoot = SdkManagedContentRoot + "/Models/Props";
        public const string SdkTexturesEntitiesRoot = SdkManagedContentRoot + "/Textures/Entities";
        public const string SdkTexturesEnvironmentRoot = SdkManagedContentRoot + "/Textures/Environment";
        public const string SdkTexturesPropsRoot = SdkManagedContentRoot + "/Textures/Props";
        public const string SdkShadersSkyRoot = SdkManagedContentRoot + "/Shaders/Environment/Sky";
        public const string SdkEntityKitsRoot = SdkManagedContentRoot + "/Prefabs/Entities/Kits";
        public const string CustomContentRoot = "Assets/CustomContent";

        public const string SdkContentManifestFileName = "sdk-content-manifest.json";
        public const int SdkContentSchemaVersion = 3;

        public const string TempSceneDir = "Assets/__TempExport__";
        public const string TempCompiledSceneDir = TempSceneDir + "/Compiled";
        public const string AddonManifestFileName = "addon.json";
        public const string CompilationReceiptFileName = "compile-receipt.json";
        public const string ExportReportFileName = "export-report.json";
        public static string DefaultModeId => SdkGame.Current.DefaultModeId;
        public const string MapsFolder = "maps";
        public const string MapLabel = "map-definition";
        public const string BaseGroupName = "Base";
        public const string TempGroupPrefix = "Temp_MapExport_";

        public const string AddonsToken = "Addons";
        public const string ScratchBuild = "Library/MapExporterBuild/[BuildTarget]";

        public const string RemoteBuildVar = "Remote.BuildPath";
        public const string RemoteLoadVar = "Remote.LoadPath";
        public const string LocalBuildVar = "Local.BuildPath";
        public const string LocalLoadVar = "Local.LoadPath";

        public static SdkAssetRootMapping[] SdkAssetRootMappings => SdkGame.Current.AssetRoots;

        public static string[] SdkSourceAssetRoots => SdkAssetRootMappings.Select(mapping => mapping.SourceRoot).ToArray();

        public static string[] SdkInstalledAssetRoots => SdkAssetRootMappings.Select(mapping => mapping.InstalledRoot).ToArray();

        public static bool IsCurrentSdkContentSchemaVersion(int schemaVersion)
        {
            return schemaVersion == SdkContentSchemaVersion;
        }
    }

    public readonly struct SdkAssetRootMapping
    {
        public SdkAssetRootMapping(string sourceRoot, string installedRoot)
        {
            SourceRoot = sourceRoot ?? string.Empty;
            InstalledRoot = installedRoot ?? string.Empty;
        }

        public string SourceRoot { get; }
        public string InstalledRoot { get; }
    }
}
