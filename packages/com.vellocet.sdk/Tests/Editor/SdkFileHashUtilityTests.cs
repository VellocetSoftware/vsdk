// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Tests
{
    public sealed class SdkFileHashUtilityTests
    {
        private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        [TestCase("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
        [TestCase("abc", AbcHash)]
        public void HashMatchesKnownSha256Vectors(string input, string expected)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            Assert.That(SdkFileHashUtility.TryComputeSha256(stream, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(55)]
        [TestCase(56)]
        [TestCase(63)]
        [TestCase(64)]
        [TestCase(65)]
        [TestCase(65535)]
        [TestCase(65536)]
        [TestCase(65537)]
        [TestCase(1000000)]
        public void StreamingHashMatchesManagedSha256AtBlockBoundaries(int length)
        {
            var bytes = new byte[length];
            new Random(42).NextBytes(bytes);
            using var managed = SHA256.Create();
            var expected = string.Concat(managed.ComputeHash(bytes).Select(value => value.ToString("x2")));
            using var stream = new MemoryStream(bytes);
            stream.Position = 3;

            Assert.That(SdkFileHashUtility.TryComputeSha256(stream, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(stream.Position, Is.EqualTo(3));
            Assert.That(stream.CanRead, Is.True);
        }

        [Test]
        public void FileHashMatchesKnownVectorAndClosesHandle()
        {
            var path = Path.Combine(Path.GetTempPath(), "aniv-hash-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(path, "abc", new UTF8Encoding(false));
                Assert.That(SdkFileHashUtility.TryComputeSha256(path, out var actual), Is.True);
                Assert.That(actual, Is.EqualTo(AbcHash));
                using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void FileHashSupportsPathsLongerThanWindowsMaxPath()
        {
            var root = Path.Combine(Path.GetTempPath(), "sdk-hash-" + Guid.NewGuid().ToString("N"));
            var directory = Path.Combine(root, new string('a', 100), new string('b', 100), new string('c', 100));
            var path = Path.Combine(directory, "payload.bin");
            var ioRoot = Path.DirectorySeparatorChar == '\\' ? @"\\?\" + root : root;
            var ioDirectory = Path.DirectorySeparatorChar == '\\' ? @"\\?\" + directory : directory;
            try
            {
                Directory.CreateDirectory(ioDirectory);
                File.WriteAllText(Path.Combine(ioDirectory, "payload.bin"), "abc", new UTF8Encoding(false));
                Assert.That(path.Length, Is.GreaterThan(260));
                Assert.That(SdkFileHashUtility.TryComputeSha256(path, out var actual), Is.True);
                Assert.That(actual, Is.EqualTo(AbcHash));
            }
            finally
            {
                if (Directory.Exists(ioRoot)) Directory.Delete(ioRoot, true);
            }
        }

        [Test]
        public void NonSeekableStreamsWithShortReadsHashAllRemainingBytes()
        {
            using var stream = new ShortReadStream(Encoding.UTF8.GetBytes("prefixabc"));
            stream.Position = 6;
            Assert.That(SdkFileHashUtility.TryComputeSha256(stream, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(AbcHash));
            Assert.That(stream.CanRead, Is.True);
        }

        [Test]
        public void ReadFailuresReturnNoDigestAndRestoreStreamPosition()
        {
            using var stream = new FailingReadStream();
            stream.Position = 2;
            Assert.That(SdkFileHashUtility.TryComputeSha256(stream, out var actual), Is.False);
            Assert.That(actual, Is.Empty);
            Assert.That(stream.Position, Is.EqualTo(2));
        }

        [Test]
        public void MissingFilesAndClosedStreamsReturnNoDigest()
        {
            Assert.That(SdkFileHashUtility.TryComputeSha256(Path.Combine(Path.GetTempPath(),
                Guid.NewGuid().ToString("N")), out var missing), Is.False);
            Assert.That(missing, Is.Empty);
            var stream = new MemoryStream();
            stream.Dispose();
            Assert.That(SdkFileHashUtility.TryComputeSha256(stream, out var unreadable), Is.False);
            Assert.That(unreadable, Is.Empty);
        }

        private sealed class ShortReadStream : MemoryStream
        {
            public ShortReadStream(byte[] bytes) : base(bytes) { }
            public override bool CanSeek => false;

            public override int Read(byte[] buffer, int offset, int count)
            {
                return base.Read(buffer, offset, Math.Min(count, 2));
            }
        }

        private sealed class FailingReadStream : MemoryStream
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new IOException("Test read failure.");
            }
        }
    }
}
