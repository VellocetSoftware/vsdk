// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.ContentLinking
{
    internal static class SdkManagedFileVerifier
    {
        private static readonly Regex UniversalAssetVersionDocument = new(
            "^--- !u!114 &-?[0-9]+\\r?\\nMonoBehaviour:\\r?\\n  m_ObjectHideFlags: 11\\r?\\n  m_CorrespondingSourceObject: \\{fileID: 0\\}\\r?\\n  m_PrefabInstance: \\{fileID: 0\\}\\r?\\n  m_PrefabAsset: \\{fileID: 0\\}\\r?\\n  m_GameObject: \\{fileID: 0\\}\\r?\\n  m_Enabled: 1\\r?\\n  m_EditorHideFlags: 0\\r?\\n  m_Script: \\{fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3\\}\\r?\\n  m_Name:[ \\t]*\\r?\\n  m_EditorClassIdentifier: Unity\\.RenderPipelines\\.Universal\\.Editor::UnityEditor\\.Rendering\\.Universal\\.AssetVersion\\r?\\n  version: [0-9]+\\r?\\n?",
            RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        private static readonly UTF8Encoding StrictUtf8 = new(
            false,
            true
        );

        internal static bool DoesFileMatch(string absolutePath, SdkContentManifestEntry entry)
        {
            if (
                string.IsNullOrWhiteSpace(absolutePath)
                || !File.Exists(absolutePath)
                || entry == null
            )
                return false;
            try
            {
                if (
                    entry.bytes >= 0
                    && new FileInfo(absolutePath).Length == entry.bytes
                    && SdkFileHashUtility.TryComputeSha256(absolutePath, out var sha)
                    && string.Equals(sha, entry.sha256, StringComparison.OrdinalIgnoreCase)
                )
                    return true;
                if (!IsUnityMaterial(entry.relativePath)) return false;
                return DoesBytesMatch(File.ReadAllBytes(absolutePath), entry);
            }
            catch
            {
                return false;
            }
        }

        internal static bool DoesBytesMatch(byte[] contents, SdkContentManifestEntry entry)
        {
            if (
                contents == null
                || entry == null
                || !SdkContentOwnershipStore.IsSha256(entry.sha256)
            )
                return false;
            if (DoesExactBytesMatch(contents, entry)) return true;
            byte[] canonicalContents;
            return IsUnityMaterial(entry.relativePath)
                   && TryRemoveUniversalAssetVersionDocument(contents, out canonicalContents)
                   && DoesExactBytesMatch(canonicalContents, entry);
        }

        internal static bool TryRemoveUniversalAssetVersionDocument(
            byte[] contents,
            out byte[] canonicalContents
        )
        {
            canonicalContents = Array.Empty<byte>();
            if (contents == null || contents.Length == 0) return false;
            try
            {
                var text = StrictUtf8.GetString(contents);
                var matchCollection = UniversalAssetVersionDocument.Matches(text);
                if (matchCollection.Count != 1) return false;
                var match = matchCollection[0];
                canonicalContents = StrictUtf8.GetBytes(text.Remove(match.Index, match.Length));
                return true;
            }
            catch
            {
                canonicalContents = Array.Empty<byte>();
                return false;
            }
        }

        private static bool DoesExactBytesMatch(byte[] contents, SdkContentManifestEntry entry)
        {
            if (entry.bytes >= 0 && contents.LongLength != entry.bytes) return false;
            return string.Equals(
                ComputeSha256(contents),
                entry.sha256,
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static string ComputeSha256(byte[] contents)
        {
            using var sHA = SHA256.Create();
            return BitConverter
                .ToString(sHA.ComputeHash(contents))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static bool IsUnityMaterial(string relativePath)
        {
            return !string.IsNullOrWhiteSpace(relativePath)
                   && relativePath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase);
        }
    }
}