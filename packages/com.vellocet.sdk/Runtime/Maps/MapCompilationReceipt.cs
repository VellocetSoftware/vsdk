// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using System.Text;
using UnityEngine;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Maps
{
    [Serializable]
    public sealed class MapCompilationReceipt
    {
        public const int CurrentSchemaVersion = 10;
        public static bool IsSupportedSchemaVersion(int version) => version == CurrentSchemaVersion;
        public const string AddonContentKind = "addon";
        public const string BaseContentKind = "base";
        // Retained engine scene with original asset references. Distribution policy runs when packaging it.
        public const string VertexContentKind = "vertex";
        public const string SealAlgorithmSha256 = "sha256";

        public int schemaVersion = CurrentSchemaVersion;
        public string mapId;
        public string mapTitle;
        public string sceneAddress;
        public string contentKind;
        public string distributionProfile;
        public string distributionPolicy;
        public string buildId;
        public string createdUtc;
        public string compilerVersion;
        public string audioRuntime;
        public string unityVersion;
        public string sourceScenePath;
        public string sourceSceneGuid;
        public string sourceSceneHash;
        public string sourceDependenciesHash;
        public string contentRevision;
        public string compiledScenePath;
        public string compiledSceneHash;
        public string spatialGraphAddress;
        public string vsigSourcePath;
        public string vsigSourceHash;
        public string vsigProgramHash;
        public string lightmapQuality;
        public string lightingSettingsPath;
        public bool skipLightingBake;
        public bool skipSpatialOctreeBake;
        public CompilePassRecord[] passes = Array.Empty<CompilePassRecord>();
        public PlatformArtifact[] platforms = Array.Empty<PlatformArtifact>();
        public string sealAlgorithm = SealAlgorithmSha256;
        public string seal;

        [Serializable]
        public sealed class CompilePassRecord
        {
            public string name;
            public string status;
            public string message;
        }

        [Serializable]
        public sealed class PlatformArtifact
        {
            public string name;
            public string role;
            public string catalog;
            public ArtifactFile[] files = Array.Empty<ArtifactFile>();
        }

        [Serializable]
        public sealed class ArtifactFile
        {
            public string path;
            public long size;
            public string sha256;
        }
    }

    public static class MapCompilationReceiptUtility
    {
        public static bool TryLoad(string path, out MapCompilationReceipt receipt)
        {
            receipt = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return false;

                receipt = JsonUtility.FromJson<MapCompilationReceipt>(json);
                return receipt != null;
            }
            catch
            {
                receipt = null;
                return false;
            }
        }

        public static string Serialize(MapCompilationReceipt receipt, bool prettyPrint = true)
        {
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));

            return JsonUtility.ToJson(receipt, prettyPrint);
        }

        public static string ComputeSeal(MapCompilationReceipt receipt)
        {
            if (receipt == null || !MapCompilationReceipt.IsSupportedSchemaVersion(receipt.schemaVersion))
                return string.Empty;

            var payload = BuildCanonicalPayload(receipt);
            var bytes = Encoding.UTF8.GetBytes(payload);
            using var stream = new MemoryStream(bytes, false);
            if (!SdkFileHashUtility.TryComputeSha256(stream, out var hash))
                return string.Empty;
            return hash;
        }

        public static void Seal(MapCompilationReceipt receipt)
        {
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));
            if (!MapCompilationReceipt.IsSupportedSchemaVersion(receipt.schemaVersion))
                throw new InvalidOperationException(
                    $"Compilation receipt schema '{receipt.schemaVersion}' requires rebuilding with the current SDK. Expected '{MapCompilationReceipt.CurrentSchemaVersion}'.");

            receipt.sealAlgorithm = MapCompilationReceipt.SealAlgorithmSha256;
            receipt.seal = ComputeSeal(receipt);
        }

        public static bool HasValidSeal(MapCompilationReceipt receipt)
        {
            if (receipt == null)
                return false;
            if (!MapCompilationReceipt.IsSupportedSchemaVersion(receipt.schemaVersion))
                return false;
            if (!string.Equals(receipt.sealAlgorithm, MapCompilationReceipt.SealAlgorithmSha256,
                    StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrWhiteSpace(receipt.seal))
                return false;

            var expected = ComputeSeal(receipt);
            return string.Equals(expected, receipt.seal, StringComparison.OrdinalIgnoreCase);
        }

        public static string ComputeContentRevision(MapCompilationReceipt receipt)
        {
            if (receipt == null || string.IsNullOrWhiteSpace(receipt.sourceDependenciesHash))
                throw new ArgumentException("A content revision requires the map's source dependency hash.", nameof(receipt));
            var builder = new StringBuilder();
            Append(builder, receipt.mapId);
            Append(builder, receipt.compilerVersion);
            Append(builder, receipt.audioRuntime);
            Append(builder, receipt.sourceDependenciesHash);
            Append(builder, receipt.vsigProgramHash);
            Append(builder, receipt.lightmapQuality);
            Append(builder, receipt.skipLightingBake ? "1" : "0");
            Append(builder, receipt.skipSpatialOctreeBake ? "1" : "0");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()), false);
            if (!SdkFileHashUtility.TryComputeSha256(stream, out var hash))
                throw new IOException("Could not hash the map revision.");
            return hash;
        }

        private static string BuildCanonicalPayload(MapCompilationReceipt receipt)
        {
            var builder = new StringBuilder(1024);
            Append(builder, receipt.schemaVersion.ToString());
            Append(builder, receipt.mapId);
            Append(builder, receipt.mapTitle);
            Append(builder, receipt.sceneAddress);
            Append(builder, receipt.contentKind);
            Append(builder, receipt.distributionProfile);
            Append(builder, receipt.distributionPolicy);
            Append(builder, receipt.buildId);
            Append(builder, receipt.createdUtc);
            Append(builder, receipt.compilerVersion);
            Append(builder, receipt.audioRuntime);
            Append(builder, receipt.unityVersion);
            Append(builder, receipt.sourceScenePath);
            Append(builder, receipt.sourceSceneGuid);
            Append(builder, receipt.sourceSceneHash);
            Append(builder, receipt.sourceDependenciesHash);
            Append(builder, receipt.contentRevision);
            Append(builder, receipt.compiledScenePath);
            Append(builder, receipt.compiledSceneHash);
            Append(builder, receipt.spatialGraphAddress);
            Append(builder, receipt.vsigSourcePath);
            Append(builder, receipt.vsigSourceHash);
            Append(builder, receipt.vsigProgramHash);
            Append(builder, receipt.lightmapQuality);
            Append(builder, receipt.lightingSettingsPath);
            Append(builder, receipt.skipLightingBake ? "1" : "0");
            Append(builder, receipt.skipSpatialOctreeBake ? "1" : "0");

            foreach (var pass in receipt.passes ?? Array.Empty<MapCompilationReceipt.CompilePassRecord>())
            {
                Append(builder, pass?.name);
                Append(builder, pass?.status);
                Append(builder, pass?.message);
            }

            foreach (var platform in receipt.platforms ?? Array.Empty<MapCompilationReceipt.PlatformArtifact>())
            {
                Append(builder, platform?.name);
                Append(builder, platform?.role);
                Append(builder, platform?.catalog);

                foreach (var file in platform?.files ?? Array.Empty<MapCompilationReceipt.ArtifactFile>())
                {
                    Append(builder, file?.path);
                    Append(builder, file?.size.ToString() ?? "0");
                    Append(builder, file?.sha256);
                }
            }

            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            builder.Append(value ?? string.Empty);
            builder.Append('\n');
        }
    }
}
