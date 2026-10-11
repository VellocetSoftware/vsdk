// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Vellocet.Sdk.Utilities
{
    public static class SdkFileHashUtility
    {
        public static bool TryComputeSha256(string filePath, out string sha256)
        {
            sha256 = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                using var stream = File.OpenRead(filePath);
                return TryComputeSha256FromStream(stream, out sha256);
            }
            catch
            {
                sha256 = string.Empty;
                return false;
            }
        }

        public static bool TryComputeSha256(Stream stream, out string sha256)
        {
            sha256 = string.Empty;
            if (stream == null || !stream.CanRead)
                return false;

            long? originalPosition = null;
            try
            {
                if (stream.CanSeek)
                {
                    originalPosition = stream.Position;
                    stream.Position = 0L;
                }

                return TryComputeSha256FromStream(stream, out sha256);
            }
            catch
            {
                sha256 = string.Empty;
                return false;
            }
            finally
            {
                if (originalPosition.HasValue && stream.CanSeek)
                    stream.Position = originalPosition.Value;
            }
        }

        private static bool TryComputeSha256FromStream(Stream stream, out string sha256)
        {
            sha256 = string.Empty;
            if (stream == null || !stream.CanRead)
                return false;

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            sha256 = string.Concat(hash.Select(b => b.ToString("x2")));
            return !string.IsNullOrWhiteSpace(sha256);
        }
    }
}