// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.ContentLinking
{
    /// <summary>
    ///     Verifies managed SDK files once, reuses stable observations within a sync, and persists
    ///     verified file-system stamps through the ownership receipt for subsequent no-op syncs.
    /// </summary>
    internal sealed class SdkContentVerificationCache
    {
        private readonly Dictionary<string, CachedObservation> _observations = new(
            StringComparer.OrdinalIgnoreCase
        );

        private readonly IReadOnlyDictionary<
            string,
            SdkContentOwnershipStore.VerificationRecord
        > _persistedRecords;

        private readonly Dictionary<
            string,
            SdkContentOwnershipStore.VerificationRecord
        > _verifiedRecords = new(StringComparer.OrdinalIgnoreCase);

        internal SdkContentVerificationCache(
            IReadOnlyDictionary<
                string,
                SdkContentOwnershipStore.VerificationRecord
            > persistedRecords
        )
        {
            _persistedRecords =
                persistedRecords
                ?? new Dictionary<string, SdkContentOwnershipStore.VerificationRecord>(
                    StringComparer.OrdinalIgnoreCase
                );
        }

        internal int FastVerifiedFileCount { get; private set; }

        internal int HashedFileCount { get; private set; }

        internal int MemoizedVerificationCount { get; private set; }

        internal bool RecordsChanged { get; private set; }

        internal bool DoesFileMatch(string absolutePath, SdkContentManifestEntry entry)
        {
            if (
                string.IsNullOrWhiteSpace(absolutePath)
                || entry == null
                || !TryGetFileStamp(absolutePath, out var initialStamp)
            )
                return false;

            var normalizedAbsolutePath = Path.GetFullPath(absolutePath);
            var observationKey = BuildObservationKey(normalizedAbsolutePath, entry.sha256);
            if (
                _observations.TryGetValue(observationKey, out var cached)
                && cached.Stamp.Equals(initialStamp)
            )
            {
                MemoizedVerificationCount++;
                return cached.Matches;
            }

            var isCanonicalDestination = IsCanonicalDestination(
                normalizedAbsolutePath,
                entry.relativePath
            );
            var matches = isCanonicalDestination && CanUseVerifiedStamp(entry, initialStamp);
            FileStamp verifiedStamp;
            if (matches)
            {
                FastVerifiedFileCount++;
                verifiedStamp = initialStamp;
            }
            else
            {
                HashedFileCount++;
                matches = SdkManagedFileVerifier.DoesFileMatch(normalizedAbsolutePath, entry);
                if (
                    !TryGetFileStamp(normalizedAbsolutePath, out verifiedStamp)
                    || !initialStamp.Equals(verifiedStamp)
                )
                    matches = false;
            }

            _observations[observationKey] = new CachedObservation(verifiedStamp, matches);
            if (matches && isCanonicalDestination)
                TrackVerifiedRecord(entry, verifiedStamp);
            return matches;
        }

        internal void Invalidate(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                return;

            var prefix = Path.GetFullPath(absolutePath) + "\0";
            foreach (
                var key in _observations
                    .Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToArray()
            )
                _observations.Remove(key);
        }

        internal IReadOnlyList<SdkContentOwnershipStore.VerificationRecord> BuildRecords(
            IEnumerable<SdkContentManifestEntry> entries
        )
        {
            var records = new List<SdkContentOwnershipStore.VerificationRecord>();
            foreach (
                var entry in (entries ?? Array.Empty<SdkContentManifestEntry>())
                .Where(entry => entry != null)
                .OrderBy(entry => entry.relativePath, StringComparer.OrdinalIgnoreCase)
            )
            {
                if (
                    !_verifiedRecords.TryGetValue(entry.relativePath, out var record)
                    || !string.Equals(
                        record.manifestSha256,
                        entry.sha256,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    continue;

                records.Add(CloneRecord(record));
            }

            return records;
        }

        internal static bool AreEntriesEquivalent(
            IReadOnlyDictionary<string, SdkContentManifestEntry> first,
            IReadOnlyDictionary<string, SdkContentManifestEntry> second
        )
        {
            if (ReferenceEquals(first, second))
                return true;
            if (first == null || second == null || first.Count != second.Count)
                return false;

            foreach (var pair in first)
                if (
                    pair.Value == null
                    || !second.TryGetValue(pair.Key, out var other)
                    || !AreEntriesEquivalent(pair.Value, other)
                )
                    return false;
            return true;
        }

        private bool CanUseVerifiedStamp(SdkContentManifestEntry entry, FileStamp actualStamp)
        {
            return _persistedRecords.TryGetValue(entry.relativePath, out var persisted)
                   && DoesVerificationRecordMatch(
                       persisted,
                       entry,
                       actualStamp.Bytes,
                       actualStamp.LastWriteUtcTicks
                   );
        }

        internal static bool DoesVerificationRecordMatch(
            SdkContentOwnershipStore.VerificationRecord record,
            SdkContentManifestEntry entry,
            long actualBytes,
            long actualLastWriteUtcTicks
        )
        {
            return record != null
                   && entry != null
                   && string.Equals(
                       SdkContentPathUtility.NormalizeProjectRelative(record.relativePath),
                       SdkContentPathUtility.NormalizeProjectRelative(entry.relativePath),
                       StringComparison.OrdinalIgnoreCase
                   )
                   && string.Equals(
                       record.manifestSha256,
                       entry.sha256,
                       StringComparison.OrdinalIgnoreCase
                   )
                   && record.fileBytes == actualBytes
                   && record.lastWriteUtcTicks == actualLastWriteUtcTicks;
        }

        private void TrackVerifiedRecord(SdkContentManifestEntry entry, FileStamp stamp)
        {
            var record = new SdkContentOwnershipStore.VerificationRecord
            {
                relativePath = entry.relativePath,
                manifestSha256 = entry.sha256?.Trim().ToLowerInvariant() ?? string.Empty,
                fileBytes = stamp.Bytes,
                lastWriteUtcTicks = stamp.LastWriteUtcTicks
            };
            _verifiedRecords[entry.relativePath] = record;
            if (
                !_persistedRecords.TryGetValue(entry.relativePath, out var persisted)
                || !string.Equals(
                    persisted.manifestSha256,
                    record.manifestSha256,
                    StringComparison.OrdinalIgnoreCase
                )
                || persisted.fileBytes != record.fileBytes
                || persisted.lastWriteUtcTicks != record.lastWriteUtcTicks
            )
                RecordsChanged = true;
        }

        private static bool IsCanonicalDestination(
            string normalizedAbsolutePath,
            string relativePath
        )
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;
            try
            {
                return string.Equals(
                    normalizedAbsolutePath,
                    Path.GetFullPath(SdkContentPathUtility.ToAbsolutePath(relativePath)),
                    StringComparison.OrdinalIgnoreCase
                );
            }
            catch
            {
                return false;
            }
        }

        internal static bool AreEntriesEquivalent(
            SdkContentManifestEntry first,
            SdkContentManifestEntry second
        )
        {
            return first != null
                   && second != null
                   && string.Equals(
                       SdkContentPathUtility.NormalizeProjectRelative(first.relativePath),
                       SdkContentPathUtility.NormalizeProjectRelative(second.relativePath),
                       StringComparison.OrdinalIgnoreCase
                   )
                   && first.bytes == second.bytes
                   && string.Equals(first.sha256, second.sha256, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetFileStamp(string absolutePath, out FileStamp stamp)
        {
            stamp = default;
            try
            {
                var file = new FileInfo(absolutePath);
                if (!file.Exists)
                    return false;
                stamp = new FileStamp(file.Length, file.LastWriteTimeUtc.Ticks);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildObservationKey(string absolutePath, string sha256)
        {
            return absolutePath + "\0" + (sha256?.Trim().ToLowerInvariant() ?? string.Empty);
        }

        private static SdkContentOwnershipStore.VerificationRecord CloneRecord(
            SdkContentOwnershipStore.VerificationRecord source
        )
        {
            return new SdkContentOwnershipStore.VerificationRecord
            {
                relativePath = source.relativePath,
                manifestSha256 = source.manifestSha256,
                fileBytes = source.fileBytes,
                lastWriteUtcTicks = source.lastWriteUtcTicks
            };
        }

        private readonly struct FileStamp : IEquatable<FileStamp>
        {
            internal FileStamp(long bytes, long lastWriteUtcTicks)
            {
                Bytes = bytes;
                LastWriteUtcTicks = lastWriteUtcTicks;
            }

            internal long Bytes { get; }

            internal long LastWriteUtcTicks { get; }

            public bool Equals(FileStamp other)
            {
                return Bytes == other.Bytes && LastWriteUtcTicks == other.LastWriteUtcTicks;
            }

            public override bool Equals(object obj)
            {
                return obj is FileStamp other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Bytes.GetHashCode() * 397) ^ LastWriteUtcTicks.GetHashCode();
                }
            }
        }

        private readonly struct CachedObservation
        {
            internal CachedObservation(FileStamp stamp, bool matches)
            {
                Stamp = stamp;
                Matches = matches;
            }

            internal FileStamp Stamp { get; }

            internal bool Matches { get; }
        }
    }
}