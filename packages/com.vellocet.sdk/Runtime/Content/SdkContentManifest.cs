// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;

namespace Vellocet.Sdk
{
    [Serializable]
    public sealed class SdkContentManifest
    {
        public int schemaVersion;
        public string exportedAtUtc;
        public List<SdkContentManifestEntry> entries = new();
    }

    [Serializable]
    public sealed class SdkContentManifestEntry
    {
        public string relativePath;
        public string sha256;
        public long bytes = -1;
        public long sourceLastWriteUtcTicks = -1;
    }
}