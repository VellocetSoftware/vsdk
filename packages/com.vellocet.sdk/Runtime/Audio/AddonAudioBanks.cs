using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Utilities;
#if VELLOCET_STEREOFORM
using Vellocet.Stereoform;
#endif

namespace Vellocet.Sdk.Audio
{
    /// <summary>Map-scoped bank ownership; referenced native media survives bank retirement.</summary>
    public static class AddonAudioBanks
    {
        public const string Capability = "stereoform/2";
#if VELLOCET_STEREOFORM
        public readonly struct Entry
        {
            public readonly string Bank, Event;
            public readonly float Duration;
            public Entry(string bank, string id, double duration) { Bank = bank; Event = id; Duration = (float)duration; }
        }
        public sealed class Definition
        {
            public string Key, Directory;
            public AddonAudioCatalog Cues;
            public BankCatalog Catalog;
        }
        private sealed class Mounted { public Definition Definition; public int References; }
        private static readonly Dictionary<string, Mounted> MountedBanks = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Entry> Events = new(StringComparer.Ordinal);
        public static bool TryResolve(string cue, out Entry entry) => Events.TryGetValue(cue, out entry);

        public static Definition Validate(string root, AddonManifest manifest, AddonManifest.ContentEntry content)
        {
            if (manifest.audioRuntime != Capability || content?.audio == null ||
                !AddonHelper.TryResolvePath(root, content.audio.catalog, out var catalogPath, out _))
                throw new InvalidDataException("Rebuild this add-on with the Stereoform ANEURISM IV SDK.");
            var files = new Dictionary<string, AddonManifest.PlatformFile>(StringComparer.Ordinal);
            foreach (var file in content.audio.files ?? Array.Empty<AddonManifest.PlatformFile>())
            {
                if (file == null || !AddonFilePolicy.TryValidateFileSize(file.path, file.size, out _) ||
                    !files.TryAdd(file.path, file) || !AddonHelper.TryResolvePath(root, file.path, out var path, out _) ||
                    !CheckFile(path, file))
                    throw new InvalidDataException("Add-on bank inventory is missing, changed, or oversized.");
            }
            if (!files.ContainsKey(content.audio.catalog)) throw new InvalidDataException("Audio catalog is not inventoried.");
            var cues = JsonUtility.FromJson<AddonAudioCatalog>(File.ReadAllText(catalogPath));
            if (cues?.audioRuntime != Capability || cues.bank != "bank" || cues.cues == null || cues.cues.Length is < 1 or > 256 ||
                !AddonAudioPolicy.IsSupportedSchemaVersion(cues.schemaVersion))
                throw new InvalidDataException("Rebuild this audio catalog with the Stereoform SDK.");
            string directory = Path.Combine(Path.GetDirectoryName(catalogPath), cues.bank);
            #if UNITY_SERVER
            var bank = BankCatalog.Read(directory, false);
#else
            var bank = BankCatalog.Read(directory, true);
            bank.VerifyDecodedLimit(directory, AddonAudioPolicy.MaximumDecodedBytesPerContent);
#endif
            if (bank.contentNamespace != "addon." + manifest.id || bank.events.Length != cues.cues.Length)
                throw new InvalidDataException("Audio bank ownership does not match its add-on.");
            var expected = new HashSet<string>(StringComparer.Ordinal) { content.audio.catalog };
            foreach (var path in bank.media.Select(item => item.path).Concat(new[] { "catalog.json", "bank.json" }))
            {
                string relative = Path.GetRelativePath(root, Path.Combine(directory, path)).Replace('\\', '/');
                if (!files.ContainsKey(relative)) throw new InvalidDataException("Bank file is not inventoried: " + path);
                expected.Add(relative);
            }
            if (files.Keys.Except(expected).Any()) throw new InvalidDataException("Unexpected file in add-on audio bank.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            double decoded = 0;
            foreach (var cue in cues.cues)
            {
                if (cue == null || !AddonAudioPolicy.TryNormalizeCueId(manifest.id, cue.id, out _) || !ids.Add(cue.id) ||
                    cue.eventId != "addon." + manifest.id + "/cue/" + cue.id ||
                    !AddonAudioPolicy.TryParseTemplate(cue.template, out _) ||
                    !AddonAudioPolicy.TryResolveGain(cues.schemaVersion, cue.gain, out _))
                    throw new InvalidDataException("Invalid audio cue ownership, template, or gain.");
                var entry = bank.events.SingleOrDefault(item => item.id == cue.eventId);
                if (entry == null || Math.Abs(entry.duration - cue.duration) > .000001)
                    throw new InvalidDataException("Cue duration or event does not match its compiled bank.");
                decoded += entry.duration * 48000 * 8;
                if (decoded > AddonAudioPolicy.MaximumDecodedBytesPerContent)
                    throw new InvalidDataException("Add-on decoded audio exceeds 256 MiB.");
            }
            return new Definition { Key = "addon." + manifest.id + "/" + content.id, Directory = directory, Cues = cues, Catalog = bank };
        }

        private static bool CheckFile(string path, AddonManifest.PlatformFile file)
        {
#if UNITY_SERVER
            if (path.EndsWith(".sfmedia", StringComparison.Ordinal)) return AddonFilePolicy.IsValidSha256(file.sha256);
#endif
            return File.Exists(path) && new FileInfo(path).Length == file.size &&
                SdkFileHashUtility.TryComputeSha256(path, out var hash) && hash == file.sha256;
        }

        // Call on the game thread after validation has completed on the loading worker.
        public static IDisposable Mount(Definition definition, AudioRuntime runtime)
        {
            if (MountedBanks.TryGetValue(definition.Key, out var mounted))
            {
                if (mounted.Definition.Catalog.bankHash != definition.Catalog.bankHash)
                    throw new InvalidDataException("Two maps request different versions of the same audio bank.");
                mounted.References++;
                return new Lease(definition.Key, runtime);
            }
            foreach (var cue in definition.Cues.cues)
                if (Events.ContainsKey(cue.id)) throw new InvalidDataException("Two banks supply the same audio cue: " + cue.id);
#if !UNITY_SERVER
            if (runtime == null || !runtime.LoadBank(definition.Key, definition.Directory))
                throw new InvalidOperationException("Could not queue the map audio bank.");
#endif
            MountedBanks.Add(definition.Key, new Mounted { Definition = definition, References = 1 });
            foreach (var cue in definition.Cues.cues) Events.Add(cue.id, new Entry(definition.Key, cue.eventId, cue.duration));
            return new Lease(definition.Key, runtime);
        }
        private sealed class Lease : IDisposable
        {
            private string key;
            private readonly AudioRuntime runtime;
            public Lease(string bank, AudioRuntime host) { key = bank; runtime = host; }
            public void Dispose()
            {
                if (key == null) return;
                var mounted = MountedBanks[key];
                if (--mounted.References == 0)
                {
#if !UNITY_SERVER
                    if (runtime != null && !runtime.UnloadBank(key))
                        throw new InvalidOperationException("Could not retire the map audio bank.");
#endif
                    foreach (var cue in mounted.Definition.Cues.cues) Events.Remove(cue.id);
                    MountedBanks.Remove(key);
                }
                key = null;
            }
        }
#endif
    }
}
