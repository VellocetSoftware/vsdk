// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Vellocet.Sdk.Addons
{
    /// <summary>
    ///     Checks serialized type identities before Addressables can instantiate providers or log an
    ///     uncatchable missing-assembly error from its binary reader. Does not replace location validation.
    /// </summary>
    public static class AddonCatalogCompatibility
    {
        public const int MaximumCatalogBytes = 16 * 1024 * 1024;
        public const int MaximumLocations = 8192;
        // Addressables emits an address and a GUID key for each packaged asset.
        public const int MaximumKeys = MaximumLocations * 2;
        public const int MaximumDependencyDepth = 32;
        public const int MaximumProviderInitializers = 4096;
        private const int MaximumTypeNameCharacters = 4096;

        private const string RebuildInstruction =
            "Recompile this map with the current game SDK and publish the updated catalog and bundles.";

        public static bool TryValidateFile(string path, out string error)
        {
            try
            {
                using var stream = File.OpenRead(path);
                if (stream.Length <= 0 || stream.Length > MaximumCatalogBytes)
                    throw new InvalidDataException($"Addon catalog must be between 1 and {MaximumCatalogBytes} bytes.");
                var bytes = new byte[(int)stream.Length];
                var read = 0;
                while (read < bytes.Length)
                {
                    var count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0)
                        throw new EndOfStreamException("Addon catalog changed while reading it.");
                    read += count;
                }

                if (stream.ReadByte() != -1)
                    throw new InvalidDataException("Addon catalog changed while reading it.");
                return TryValidate(bytes, out error);
            }
            catch (Exception ex)
            {
                error = $"Addon catalog compatibility could not be checked: {ex.Message}";
                return false;
            }
        }

        public static bool TryValidate(byte[] bytes, out string error)
        {
            try
            {
                if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumCatalogBytes)
                    throw new InvalidDataException("Addon catalog is empty or exceeds the catalog size limit.");
#if ENABLE_JSON_CATALOG
                var catalog = JsonUtility.FromJson<JsonCatalogTypes>(Encoding.UTF8.GetString(bytes));
                if (catalog?.m_resourceTypes == null || catalog.m_resourceTypes.Length == 0 ||
                    catalog.m_resourceTypes.Length > MaximumLocations)
                    throw new InvalidDataException("Addon catalog has an invalid resource type table.");
                foreach (var type in catalog.m_resourceTypes)
                    ValidateType(type.m_AssemblyName, type.m_ClassName);
                ValidateInitializer(catalog.m_InstanceProviderData);
                ValidateInitializer(catalog.m_SceneProviderData);
                if (catalog.m_ResourceProviderData != null)
                {
                    if (catalog.m_ResourceProviderData.Length > MaximumProviderInitializers)
                        throw new InvalidDataException(
                            $"Addon catalog provider table has {catalog.m_ResourceProviderData.Length} entries; limit is {MaximumProviderInitializers}.");
                    foreach (var initializer in catalog.m_ResourceProviderData)
                        ValidateInitializer(initializer);
                }
#else
                new BinaryTypeReader(bytes).Validate();
#endif
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Addon catalog is incompatible or invalid: {ex.Message}";
                return false;
            }
        }

        private static Type ValidateType(string assemblyName, string className)
        {
            if (assemblyName != null) ValidateTypeName(assemblyName);
            ValidateTypeName(className);
            try
            {
                // v3 omits the runtime-specific corelib name; resolve generic arguments without loading assemblies.
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                Assembly ResolveAssembly(AssemblyName name)
                {
                    return assemblies.FirstOrDefault(candidate =>
                               candidate.GetName().Name == name.Name)
                           ?? throw new InvalidDataException($"Required assembly '{name.Name}' is unavailable.");
                }

                var qualified = assemblyName == null ? className : className + ", " + assemblyName;
                return Type.GetType(qualified, ResolveAssembly,
                           (assembly, name, ignoreCase) =>
                               (assembly ?? typeof(object).Assembly).GetType(name, false, ignoreCase), false)
                       ?? throw new InvalidDataException(
                           $"Required type '{className}' is absent from '{assemblyName ?? "corelib"}'.");
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"{ex.Message} {RebuildInstruction}", ex);
            }
        }

        private static void ValidateInitializerIdentity(string assemblyName, string className)
        {
            ValidateType(assemblyName, className);
            if ((assemblyName == null ? null : new AssemblyName(assemblyName).Name) != "Unity.ResourceManager" ||
                className is not (
                    "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider" or
                    "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider" or
                    "UnityEngine.ResourceManagement.ResourceProviders.InstanceProvider" or
                    "UnityEngine.ResourceManagement.ResourceProviders.SceneProvider"))
                throw new InvalidDataException("The add-on catalog declares a forbidden provider initializer.");
        }

        private static void ValidateTypeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumTypeNameCharacters)
                throw new InvalidDataException("Addon catalog contains an empty or oversized type identity.");
            foreach (var character in name)
                if (char.IsControl(character))
                    throw new InvalidDataException("Addon catalog contains control characters in a type identity.");
        }

#if ENABLE_JSON_CATALOG
        private static void ValidateInitializer(JsonInitializer initializer)
        {
            if (!string.IsNullOrEmpty(initializer?.m_ObjectType?.m_AssemblyName))
                ValidateInitializerIdentity(initializer.m_ObjectType.m_AssemblyName, initializer.m_ObjectType.m_ClassName);
        }

#pragma warning disable CS0649
        [Serializable] private sealed class JsonType { public string m_AssemblyName; public string m_ClassName; }
        [Serializable] private sealed class JsonInitializer { public JsonType m_ObjectType; }
        [Serializable] private sealed class JsonCatalogTypes
        {
            public JsonType[] m_resourceTypes;
            public JsonInitializer m_InstanceProviderData;
            public JsonInitializer m_SceneProviderData;
            public JsonInitializer[] m_ResourceProviderData;
        }
#pragma warning restore CS0649
#else
        /// <summary>
        ///     Version-specific Addressables binary catalog metadata (BinaryStorageBuffer).
        ///     Uses checked managed reads: Unity's deserializer logs exceptions and uses unsafe reads,
        ///     so it cannot serve as a quiet preflight for untrusted, possibly truncated catalog bytes.
        ///     Tests serialize through the installed Addressables package to detect format changes.
        /// </summary>
        private sealed class BinaryTypeReader
        {
            private const uint None = uint.MaxValue;
            private const uint Unicode = 0x80000000;
            private const uint Dynamic = 0x40000000;
            private const uint OffsetMask = 0x3fffffff;
            private const int MaximumStringParts = 128;
            private readonly byte[] _bytes;
            private readonly HashSet<uint> _locationSets = new();
            private readonly HashSet<uint> _locations = new();
            private readonly Queue<(uint Offset, int Depth)> _pending = new();
            private readonly Dictionary<uint, Type> _types = new();

            internal BinaryTypeReader(byte[] bytes)
            {
                _bytes = bytes;
            }

            internal void Validate()
            {
                RequireRange(0, 32);
#if VELLOCET_ADDRESSABLES_4
                const uint magic = 0x0de38942;
                const uint version = 3;
#else
                var magic = unchecked((uint)nameof(UnityEngine.AddressableAssets.ResourceLocators.ContentCatalogData).GetHashCode());
                const uint version = 2;
#endif
                if (ReadUInt(0) != magic || ReadUInt(4) != version)
                    throw new InvalidDataException(
                        $"Unsupported Addressables binary catalog header or version. {RebuildInstruction}");

                ReadTypeString(ReadUInt(12));
                ReadTypeString(ReadUInt(28));
                ReadInitializer(ReadUInt(16));
                ReadInitializer(ReadUInt(20));
                var providers = ReadUInt(24);
                var providerCount = ArrayCount(providers, 4, MaximumProviderInitializers, "provider table");
                for (var i = 0; i < providerCount; i++)
                    ReadInitializer(ReadUInt(providers + (uint)i * 4));

                var keys = ReadUInt(8);
                var keyCount = ArrayCount(keys, 8, MaximumKeys, "key table");
                if (keyCount == 0)
                    throw new InvalidDataException("Addon catalog contains no keys.");
                for (var i = 0; i < keyCount; i++)
                {
                    var key = keys + (uint)i * 8;
                    ReadObject(ReadUInt(key), true);
                    AddLocationSet(ReadUInt(key + 4), 0);
                }

                while (_pending.Count > 0)
                {
                    var (offset, depth) = _pending.Dequeue();
                    RequireRange(offset, 28);
                    ReadTypeString(ReadUInt(offset));
                    ReadTypeString(ReadUInt(offset + 4), '/');
                    ReadTypeString(ReadUInt(offset + 8));
                    ReadObject(ReadUInt(offset + 20), false);
                    ReadType(ReadUInt(offset + 24));
                    AddLocationSet(ReadUInt(offset + 12), depth + 1);
                }
            }

            private void AddLocationSet(uint offset, int depth)
            {
                if (offset == None || !_locationSets.Add(offset))
                    return;
                var count = ArrayCount(offset, 4, MaximumLocations, "location table");
                for (var i = 0; i < count; i++)
                {
                    var location = ReadUInt(offset + (uint)i * 4);
                    if (!_locations.Add(location))
                        continue;
                    if (_locations.Count > MaximumLocations)
                        throw new InvalidDataException(
                            $"Addon catalog has {_locations.Count} locations; limit is {MaximumLocations}.");
                    if (depth > MaximumDependencyDepth)
                        throw new InvalidDataException(
                            $"Addon catalog dependency depth is {depth}; limit is {MaximumDependencyDepth}.");
                    _pending.Enqueue((location, depth));
                }
            }

            private void ReadInitializer(uint offset)
            {
                if (offset == None)
                    return;
                RequireRange(offset, 12);
                ReadTypeString(ReadUInt(offset));
                ReadTypeString(ReadUInt(offset + 8));
                var type = ReadUInt(offset + 4);
                if (type == None) return;
                RequireRange(type, 8);
                ValidateInitializerIdentity(ReadTypeString(ReadUInt(type)), ReadTypeString(ReadUInt(type + 4)));
            }

            private void ReadObject(uint offset, bool key)
            {
                if (offset == None && !key) return;
                RequireRange(offset, 8);
                var type = ReadType(ReadUInt(offset));
                var data = ReadUInt(offset + 4);
                if (key)
                {
                    if (type == typeof(string))
                    {
                        RequireRange(data, 8);
                        ReadTypeString(ReadUInt(data), (char)(_bytes[(int)data + 4] | (_bytes[(int)data + 5] << 8)));
                    }
                    else if (type == typeof(int))
                    {
                        RequireRange(data, 4);
                    }
                    else if (type == typeof(bool))
                    {
                        RequireRange(data, 1);
                    }
                    else if (type == typeof(long))
                    {
                        RequireRange(data, 8);
                    }
                    else if (type == typeof(Hash128))
                    {
                        RequireRange(data, 16);
                    }
                    else
                    {
                        throw new InvalidDataException("The add-on catalog contains an unsupported key type.");
                    }
                }
                else if (type == typeof(AssetBundleRequestOptions))
                {
                    RequireRange(data, 20);
                    RequireRange(ReadUInt(data), 16);
                    ReadTypeString(ReadUInt(data + 4), '_');
                    RequireRange(ReadUInt(data + 16), 8);
                }
                else if (type == typeof(ProviderLoadRequestOptions))
                {
                    RequireRange(data, 12);
                    if (!string.IsNullOrEmpty(ReadTypeString(ReadUInt(data + 8), '/')))
                        throw new InvalidDataException("Add-on catalog cache paths are not supported.");
                }
                else
                {
                    throw new InvalidDataException("The add-on catalog contains unsupported provider data.");
                }
            }

            private Type ReadType(uint offset)
            {
                if (_types.TryGetValue(offset, out var known)) return known;
                RequireRange(offset, 8);
                var type = ValidateType(ReadTypeString(ReadUInt(offset)), ReadTypeString(ReadUInt(offset + 4)));
                _types.Add(offset, type);
                return type;
            }

            private string ReadTypeString(uint offset, char separator = '.')
            {
                if (offset == None) return null;
                if ((offset & Dynamic) == 0)
                    return ReadStringPart(offset);
                var parts = new List<string>();
                var length = 0;
                while (offset != None)
                {
                    if (parts.Count >= MaximumStringParts)
                        throw new InvalidDataException(
                            "Addon catalog type identity is cyclic or has too many string parts.");
                    offset &= OffsetMask;
                    RequireRange(offset, 8);
                    var part = ReadStringPart(ReadUInt(offset));
                    length += part.Length + (parts.Count == 0 ? 0 : 1);
                    if (length > MaximumTypeNameCharacters)
                        throw new InvalidDataException("Addon catalog type identity exceeds the string limit.");
                    parts.Add(part);
                    offset = ReadUInt(offset + 4);
                }

                parts.Reverse();
                return string.Join(separator.ToString(), parts);
            }

            private string ReadStringPart(uint id)
            {
                if (id == None || (id & Dynamic) != 0)
                    throw new InvalidDataException("Addon catalog contains an invalid type string reference.");
                var unicode = (id & Unicode) != 0;
                var offset = id & OffsetMask;
                var byteCount = ArrayCount(offset, unicode ? 2u : 1u, MaximumTypeNameCharacters, "string") * (unicode ? 2 : 1);
                return (unicode ? Encoding.Unicode : Encoding.ASCII).GetString(_bytes, (int)offset, byteCount);
            }

            private int ArrayCount(uint offset, uint stride, int maximum, string table)
            {
                if (offset == None)
                    return 0;
                if (offset < 4 || offset > _bytes.Length)
                    throw new InvalidDataException($"Addon catalog {table} has an invalid array offset.");
                var size = ReadUInt(offset - 4);
                if (size % stride != 0)
                    throw new InvalidDataException(
                        $"Addon catalog {table} is malformed: {size} bytes is not divisible by its {stride}-byte entry size.");
                var count = size / stride;
                if (count > maximum)
                    throw new InvalidDataException(
                        $"Addon catalog {table} has {count} entries; limit is {maximum}.");
                if ((ulong)offset + size > (ulong)_bytes.Length)
                    throw new InvalidDataException(
                        $"Addon catalog {table} is truncated: its {count} entries extend beyond the catalog buffer.");
                return (int)count;
            }

            private uint ReadUInt(uint offset)
            {
                RequireRange(offset, 4);
                var i = (int)offset;
                return (uint)(_bytes[i] | (_bytes[i + 1] << 8) | (_bytes[i + 2] << 16) | (_bytes[i + 3] << 24));
            }

            private void RequireRange(uint offset, uint size)
            {
                if ((ulong)offset + size > (ulong)_bytes.Length)
                    throw new InvalidDataException("Addon catalog contains an out-of-bounds metadata reference.");
            }
        }
#endif
    }
}
