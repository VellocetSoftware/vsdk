// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using UnityEngine;

namespace Vellocet.Sdk.Editor.Tools
{
    public static class SdkPathDefaults
    {
        public static string NormalizePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            try
            {
                return Path.GetFullPath(value.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetPackageOutputPickerStartPath(string currentPath)
        {
            var normalized = NormalizePath(currentPath);
            if (!string.IsNullOrWhiteSpace(normalized) && Directory.Exists(normalized))
                return normalized;

            var documents = NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (!string.IsNullOrWhiteSpace(documents) && Directory.Exists(documents))
                return documents;

            return NormalizePath(Path.GetDirectoryName(Application.dataPath));
        }
    }
}