// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Vellocet.Sdk.Maps;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class MapExportReportWriter
    {
        internal static string Write(string mapRoot, string mapId, string scenePath,
            MapAssetUsageReport assetUsageReport, string[] catalogs, string[] bundles,
            MapCompilationReceipt compilationReceipt)
        {
            if (string.IsNullOrWhiteSpace(mapRoot))
                return null;

            Directory.CreateDirectory(mapRoot);

            var report = new ExportReport
            {
                mapId = mapId ?? string.Empty,
                createdUtc = DateTime.UtcNow.ToString("o"),
                scenePath = scenePath ?? string.Empty,
                catalogs = (catalogs ?? Array.Empty<string>())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(Path.GetFileName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                bundles = (bundles ?? Array.Empty<string>())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(Path.GetFileName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                compilationReceiptFile = string.IsNullOrWhiteSpace(compilationReceipt?.mapId)
                    ? string.Empty
                    : Constants.CompilationReceiptFileName,
                compilationSeal = compilationReceipt?.seal ?? string.Empty,
                compilePasses = compilationReceipt?.passes ?? Array.Empty<MapCompilationReceipt.CompilePassRecord>(),
                sharedAssets = assetUsageReport?.sharedAssets ?? Array.Empty<MapAssetUsageReport.AssetRecord>(),
                packagedAssets = assetUsageReport?.packagedAssets ?? Array.Empty<MapAssetUsageReport.AssetRecord>()
            };

            var json = JsonUtility.ToJson(report, true);
            var reportPath = Path.Combine(mapRoot, Constants.ExportReportFileName);
            File.WriteAllText(reportPath, json);
            return reportPath;
        }

        [Serializable]
        internal sealed class ExportReport
        {
            public string mapId;
            public string createdUtc;
            public string scenePath;
            public string[] catalogs;
            public string[] bundles;
            public string compilationReceiptFile;
            public string compilationSeal;
            public MapCompilationReceipt.CompilePassRecord[] compilePasses;
            public MapAssetUsageReport.AssetRecord[] sharedAssets;
            public MapAssetUsageReport.AssetRecord[] packagedAssets;
        }
    }
}
#endif