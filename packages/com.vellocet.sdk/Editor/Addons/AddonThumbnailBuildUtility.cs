// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Editor.Addons
{
    internal static class AddonThumbnailBuildUtility
    {
        internal const int WorkshopWidth = 1920;
        internal const int WorkshopHeight = 1080;

        internal static bool TryValidateProjectAsset(Texture2D thumbnail, bool required,
            bool requireWorkshopDimensions, out string error)
        {
            var path = thumbnail ? AssetDatabase.GetAssetPath(thumbnail) : string.Empty;
            return TryValidateFile(path, required, requireWorkshopDimensions, out error);
        }

        internal static bool TryValidateFile(string path, bool required, bool requireWorkshopDimensions,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                if (required)
                    error =
                        $"Release packages require separate {WorkshopWidth} × {WorkshopHeight} PNG or JPEG add-on artwork.";
                return !required;
            }

            var extension = Path.GetExtension(path);
            if (!File.Exists(path) || !IsSupportedExtension(extension))
            {
                error = "Add-on artwork must be a readable PNG or JPEG file.";
                return false;
            }

            Texture2D decoded = null;
            try
            {
                decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!decoded.LoadImage(File.ReadAllBytes(path), true))
                {
                    error = "Add-on artwork could not be decoded. Use a valid PNG or JPEG file.";
                    return false;
                }

                if (requireWorkshopDimensions &&
                    (decoded.width != WorkshopWidth || decoded.height != WorkshopHeight))
                {
                    error = $"Add-on artwork is {decoded.width} × {decoded.height}. Release artwork must be " +
                            $"{WorkshopWidth} × {WorkshopHeight}.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"Add-on artwork could not be read: {exception.Message}";
                return false;
            }
            finally
            {
                if (decoded)
                    Object.DestroyImmediate(decoded);
            }
        }

        private static bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif