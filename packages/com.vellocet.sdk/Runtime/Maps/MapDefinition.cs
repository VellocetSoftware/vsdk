// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Vellocet.Sdk.Maps
{
    public sealed class MapDefinition : ScriptableObject
    {
        [Header("Debug")]
        [field: SerializeField]
        public bool Enabled { get; private set; } = true;

        public string id;
        public string displayName;
        public string author;
        public string version;
        public string defaultModeId;
        public MapMusicSet musicSet;

        [HideInInspector] public AddonMountData addon;
        public AssetReference thumbnailReference;
        [HideInInspector] public string contentKind;
        [HideInInspector] public string authoringScenePath;
        [HideInInspector] public string compiledScenePath;
        [HideInInspector] public string compilationReceiptPath;
        [HideInInspector] public string compilationSeal;
        [HideInInspector] public string contentRevision;
        [HideInInspector] public string compiledUtc;

        // Single authoritative scene key at runtime:
        // - Defaults: from sceneReference.RuntimeKey
        // - Addons: from addon.json -> maps[].scene
        [HideInInspector] public string sceneAddress;

        // SDK-managed compiled scene reference for base content.
        public AssetReference sceneReference;

        // Used only for addon catalogs (secondary locators)
        [NonSerialized] public AsyncOperationHandle<IResourceLocator> LocatorHandle;
        [NonSerialized] public string runtimeSceneName;

        public bool IsAddon => addon != null && !string.IsNullOrWhiteSpace(addon.root);

        public override string ToString()
        {
            var name = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            return $"Name: {name} | ID: {id} | Address: {sceneAddress}";
        }

        [Serializable]
        public sealed class AddonMountData
        {
            public string root;
            public string manifestPath;
            public string platformDirectory;
            public string catalogPath;
            public string thumbnailPath;
        }
    }
}