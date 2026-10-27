// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.IO;
using System.Linq;
using System.Security.Cryptography;
#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
using System.Runtime.InteropServices;
#endif

namespace Vellocet.Sdk.Utilities
{
    public static class SdkFileHashUtility
    {
        public static bool TryComputeSha256(string filePath, out string sha256)
        {
            sha256 = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                filePath = SdkFilePathUtility.ForIo(filePath);
                if (!File.Exists(filePath)) return false;
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    64 * 1024, FileOptions.SequentialScan);
                sha256 = ComputeSha256(stream);
                return true;
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

                sha256 = ComputeSha256(stream);
                return true;
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

        private static string ComputeSha256(Stream stream)
        {
#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
            // Avoid Mono's managed SHA-256 implementation on macOS.
            // CommonDigest.h defines CC_SHA256_CTX as 26 CC_LONG (uint32_t) words.
            var context = new byte[26 * sizeof(uint)];
            if (CC_SHA256_Init(context) != 1)
                throw new CryptographicException("Could not initialize SHA-256.");

            var buffer = new byte[64 * 1024];
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                if (CC_SHA256_Update(context, buffer, (uint)count) != 1)
                    throw new CryptographicException("Could not update SHA-256.");

            var hash = new byte[32];
            if (CC_SHA256_Final(hash, context) != 1)
                throw new CryptographicException("Could not finalize SHA-256.");
#else
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
#endif
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
        private const string CommonCrypto = "/usr/lib/system/libcommonCrypto.dylib";

        [DllImport(CommonCrypto, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int CC_SHA256_Init([Out] byte[] context);

        [DllImport(CommonCrypto, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int CC_SHA256_Update([In, Out] byte[] context, [In] byte[] data, uint length);

        [DllImport(CommonCrypto, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int CC_SHA256_Final([Out] byte[] hash, [In, Out] byte[] context);
#endif
    }
}
