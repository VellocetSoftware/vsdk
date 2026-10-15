// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;

namespace Vellocet.Sdk.Addons
{
    [Serializable]
    public sealed class AddonManifest
    {
        public const int CurrentSchemaVersion = 9;
        public const string MapContentKind = "maps";
        public const string ModContentKind = "mods";
        public const string AudioContentKind = "audio";
        public const string MediaContentKind = "media";
        public const string ModManifestFileName = "mod.json";

        public int schemaVersion;
        public string gameId;
        public string audioRuntime;
        public string id;
        public string title;
        public string author;

        /// <summary>Addon-level artwork used by catalog and publishing surfaces, separate from map imagery.</summary>
        public string addonThumbnail;

        public string version;
        public string distributionProfile;
        public string distributionPolicy;
        public string buildId;
        public string createdUtc;
        public string sdkVersion;
        public string unityVersion;

        /// <summary>Addon ids which must be selected and enabled before this addon.</summary>
        public string[] requires;

        public ContentEntry[] contents;

        [Serializable]
        public sealed class ContentEntry
        {
            public string kind;
            public string id;
            public string title;
            public MapContent map;
            public ModContent mod;
            public AudioContent audio;
            public MediaContent media;
        }

        [Serializable]
        public sealed class ModContent
        {
            /// <summary>Path to the server mod manifest, relative to the addon root.</summary>
            public string manifest;
        }

        [Serializable]
        public sealed class MapContent
        {
            public string scene;
            public string defaultModeId;
            public string musicSet;

            /// <summary>Image for this map in the in-game map selection and loading UI.</summary>
            public string thumbnail;

            public string compiledReceipt;
            public PlatformCatalog[] platforms;
        }

        [Serializable]
        public sealed class AudioContent
        {
            /// <summary>Platform-neutral cue catalog, relative to the addon root.</summary>
            public string catalog;

            /// <summary>Catalog and source-audio files shipped exactly as authored.</summary>
            public PlatformFile[] files;
        }

        [Serializable]
        public sealed class MediaContent
        {
            public string catalog;
            public PlatformFile[] files;
        }

        [Serializable]
        public sealed class PlatformCatalog
        {
            public string name;
            public string role;
            public string catalog;
            public PlatformFile[] files;
        }

        [Serializable]
        public sealed class PlatformFile
        {
            public string path;
            public long size;
            public string sha256;
        }
    }
}
