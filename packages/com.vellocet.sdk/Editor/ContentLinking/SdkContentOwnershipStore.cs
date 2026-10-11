// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.ContentLinking
{
    internal static class SdkContentOwnershipStore
    {
        internal const string StateProjectRelativePath =
            "ProjectSettings/VellocetSdkContentOwnership.json";

        private const int StateSchemaVersion = 3;

        internal static bool StateExists =>
            File.Exists(
                SdkContentPathUtility.ToAbsolutePath(
                    "ProjectSettings/VellocetSdkContentOwnership.json"
                )
            );

        internal static bool TryLoad(
            out IReadOnlyDictionary<string, SdkContentManifestEntry> entries,
            out bool stateExists,
            out string error
        )
        {
            return TryLoadWithVerification(out entries, out _, out stateExists, out error);
        }

        internal static bool TryLoadWithVerification(
            out IReadOnlyDictionary<string, SdkContentManifestEntry> entries,
            out IReadOnlyDictionary<string, VerificationRecord> verificationRecords,
            out bool stateExists,
            out string error
        )
        {
            var dictionary = new Dictionary<string, SdkContentManifestEntry>(
                StringComparer.OrdinalIgnoreCase
            );
            var verificationDictionary = new Dictionary<string, VerificationRecord>(
                StringComparer.OrdinalIgnoreCase
            );
            entries = dictionary;
            verificationRecords = verificationDictionary;
            error = string.Empty;
            var text = SdkContentPathUtility.ToAbsolutePath(
                "ProjectSettings/VellocetSdkContentOwnership.json"
            );
            stateExists = File.Exists(text);
            if (!stateExists)
                return true;
            try
            {
                var ownershipState = JsonUtility.FromJson<OwnershipState>(File.ReadAllText(text));
                if (ownershipState == null)
                {
                    error = "SDK content ownership state is invalid JSON: " + text;
                    return false;
                }

                if (ownershipState.schemaVersion != StateSchemaVersion)
                {
                    error =
                        $"Unsupported SDK ownership schema '{ownershipState.schemaVersion}' in '{text}'. "
                        + $"Expected '{StateSchemaVersion}'.";
                    return false;
                }

                foreach (var item in ownershipState.entries ?? new List<SdkContentManifestEntry>())
                    if (item != null)
                    {
                        var text2 = SdkContentPathUtility.NormalizeProjectRelative(
                            item.relativePath
                        );
                        if (
                            string.IsNullOrWhiteSpace(text2)
                            || !text2.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                            || !SdkContentPathUtility.IsInstalledSdkAssetPath(text2)
                        )
                        {
                            error =
                                "SDK ownership state contains an invalid path: '"
                                + item.relativePath
                                + "'.";
                            return false;
                        }

                        if (item.bytes < 0 || !IsSha256(item.sha256))
                        {
                            error =
                                "SDK ownership state contains invalid file metadata: '"
                                + text2
                                + "'.";
                            return false;
                        }

                        if (dictionary.ContainsKey(text2))
                        {
                            error =
                                "SDK ownership state contains a duplicate path: '" + text2 + "'.";
                            return false;
                        }

                        dictionary.Add(text2, CloneEntry(item, text2));
                    }

                if (dictionary.Count == 0)
                {
                    error = "SDK content ownership state contains no entries.";
                    return false;
                }

                foreach (var key2 in dictionary.Keys)
                {
                    string text3;
                    if (!key2.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        text3 = key2 + ".meta";
                    }
                    else
                    {
                        var text4 = key2;
                        var length = ".meta".Length;
                        text3 = text4.Substring(0, text4.Length - length);
                    }

                    var key = text3;
                    if (dictionary.ContainsKey(key))
                        continue;
                    error =
                        "SDK content ownership entry '"
                        + key2
                        + "' is missing its paired Unity asset file.";
                    return false;
                }

                foreach (
                    var record in ownershipState.verifiedFiles ?? new List<VerificationRecord>()
                )
                {
                    if (record == null)
                        continue;

                    var relativePath = SdkContentPathUtility.NormalizeProjectRelative(
                        record.relativePath
                    );
                    if (
                        string.IsNullOrWhiteSpace(relativePath)
                        || !dictionary.TryGetValue(relativePath, out var ownedEntry)
                        || record.fileBytes < 0
                        || record.lastWriteUtcTicks <= 0
                        || !IsSha256(record.manifestSha256)
                        || !string.Equals(
                            record.manifestSha256,
                            ownedEntry.sha256,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        error =
                            "SDK ownership state contains an invalid verification record: '"
                            + record.relativePath
                            + "'.";
                        return false;
                    }

                    if (verificationDictionary.ContainsKey(relativePath))
                    {
                        error =
                            "SDK ownership state contains a duplicate verification record: '"
                            + relativePath
                            + "'.";
                        return false;
                    }

                    verificationDictionary.Add(
                        relativePath,
                        CloneVerificationRecord(record, relativePath)
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "Failed to read SDK content ownership state '" + text + "': " + ex.Message;
                return false;
            }
        }

        internal static bool TrySave(
            IEnumerable<SdkContentManifestEntry> sourceEntries,
            out string error
        )
        {
            return TrySave(sourceEntries, Array.Empty<VerificationRecord>(), out error);
        }

        internal static bool TrySave(
            IEnumerable<SdkContentManifestEntry> sourceEntries,
            IEnumerable<VerificationRecord> verificationRecords,
            out string error
        )
        {
            error = string.Empty;
            var text = SdkContentPathUtility.ToAbsolutePath(
                "ProjectSettings/VellocetSdkContentOwnership.json"
            );
            var text2 = text + ".tmp";
            var text3 = text + ".bak";
            try
            {
                var normalizedEntries = (
                    from entry in (sourceEntries ?? Array.Empty<SdkContentManifestEntry>())
                        .Where(entry => entry != null)
                        .OrderBy(entry => entry.relativePath, StringComparer.OrdinalIgnoreCase)
                    select CloneEntry(
                        entry,
                        SdkContentPathUtility.NormalizeProjectRelative(entry.relativePath)
                    )
                ).ToList();
                var entriesByPath = normalizedEntries.ToDictionary(
                    entry => entry.relativePath,
                    StringComparer.OrdinalIgnoreCase
                );
                var normalizedVerificationRecords = new List<VerificationRecord>();
                var seenVerificationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (
                    var record in (verificationRecords ?? Array.Empty<VerificationRecord>())
                    .Where(record => record != null)
                    .OrderBy(record => record.relativePath, StringComparer.OrdinalIgnoreCase)
                )
                {
                    var relativePath = SdkContentPathUtility.NormalizeProjectRelative(
                        record.relativePath
                    );
                    if (
                        !entriesByPath.TryGetValue(relativePath, out var entry)
                        || record.fileBytes < 0
                        || record.lastWriteUtcTicks <= 0
                        || !IsSha256(record.manifestSha256)
                        || !string.Equals(
                            record.manifestSha256,
                            entry.sha256,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        throw new InvalidDataException(
                            "Cannot save invalid SDK content verification record '"
                            + relativePath
                            + "'."
                        );
                    if (!seenVerificationPaths.Add(relativePath))
                        throw new InvalidDataException(
                            "Cannot save duplicate SDK content verification record '"
                            + relativePath
                            + "'."
                        );

                    normalizedVerificationRecords.Add(
                        CloneVerificationRecord(record, relativePath)
                    );
                }

                var ownershipState = new OwnershipState
                {
                    schemaVersion = StateSchemaVersion,
                    updatedAtUtc = DateTime.UtcNow.ToString("O"),
                    entries = normalizedEntries,
                    verifiedFiles = normalizedVerificationRecords
                };
                var directoryName = Path.GetDirectoryName(text);
                if (!string.IsNullOrWhiteSpace(directoryName))
                    Directory.CreateDirectory(directoryName);
                File.WriteAllText(text2, JsonUtility.ToJson(ownershipState, true));
                if (File.Exists(text))
                {
                    DeleteFileIfExists(text3);
                    try
                    {
                        File.Replace(text2, text, text3);
                    }
                    catch
                    {
                        File.Copy(text2, text, true);
                        DeleteFileIfExists(text2);
                    }

                    TryDeleteTemporaryFile(text3);
                }
                else
                {
                    File.Move(text2, text);
                }

                return true;
            }
            catch (Exception ex)
            {
                TryDeleteTemporaryFile(text2);
                error = "Failed to write SDK content ownership state '" + text + "': " + ex.Message;
                return false;
            }
        }

        internal static bool TryGetOwnedAssetPaths(out HashSet<string> assetPaths)
        {
            assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!TryLoad(out var entries, out var stateExists, out _) || !stateExists)
                return false;
            foreach (var key in entries.Keys)
                if (!key.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    assetPaths.Add(key);

            return true;
        }

        internal static bool TryValidateState(out string error)
        {
            if (!TryLoad(out _, out var stateExists, out error))
                return false;
            if (stateExists)
                return true;
            error =
                "SDK content ownership receipt is missing: ProjectSettings/VellocetSdkContentOwnership.json";
            return false;
        }

        internal static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                return false;
            foreach (var c in value)
            {
                var flag = c >= '0' && c <= '9';
                var flag2 = c >= 'a' && c <= 'f';
                var flag3 = c >= 'A' && c <= 'F';
                if (!flag && !flag2 && !flag3)
                    return false;
            }

            return true;
        }

        private static SdkContentManifestEntry CloneEntry(
            SdkContentManifestEntry source,
            string relativePath
        )
        {
            return new SdkContentManifestEntry
            {
                relativePath = relativePath,
                sha256 = source.sha256?.Trim().ToLowerInvariant() ?? string.Empty,
                bytes = source.bytes,
                sourceLastWriteUtcTicks = source.sourceLastWriteUtcTicks
            };
        }

        private static VerificationRecord CloneVerificationRecord(
            VerificationRecord source,
            string relativePath
        )
        {
            return new VerificationRecord
            {
                relativePath = relativePath,
                manifestSha256 = source.manifestSha256?.Trim().ToLowerInvariant() ?? string.Empty,
                fileBytes = source.fileBytes,
                lastWriteUtcTicks = source.lastWriteUtcTicks
            };
        }

        private static void DeleteFileIfExists(string absolutePath)
        {
            if (File.Exists(absolutePath))
            {
                File.SetAttributes(absolutePath, FileAttributes.Normal);
                File.Delete(absolutePath);
            }
        }

        private static void TryDeleteTemporaryFile(string absolutePath)
        {
            try
            {
                DeleteFileIfExists(absolutePath);
            }
            catch
            {
            }
        }

        [Serializable]
        private sealed class OwnershipState
        {
            public int schemaVersion = StateSchemaVersion;

            public string updatedAtUtc = string.Empty;

            public List<SdkContentManifestEntry> entries = new();

            public List<VerificationRecord> verifiedFiles = new();
        }

        [Serializable]
        internal sealed class VerificationRecord
        {
            public string relativePath = string.Empty;

            public string manifestSha256 = string.Empty;

            public long fileBytes = -1;

            public long lastWriteUtcTicks = -1;
        }
    }
}