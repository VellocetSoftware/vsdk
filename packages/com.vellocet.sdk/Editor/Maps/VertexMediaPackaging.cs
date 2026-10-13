using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Vertex.Contracts;
using Vertex.Unity.Editor;
using Vellocet.Sdk.Addons;
using Vellocet.Sdk.Utilities;

namespace Vellocet.Sdk.Editor.Maps
{
    internal static class VertexMediaPackaging
    {
        internal static string[] Requirements(BuildJob job, Func<string, string> pathFor) =>
            (job.Content ?? Array.Empty<BuildContent>()).Where(content => !content.External)
                .SelectMany(content => ContentManifest.Load(pathFor(content.Path)).Requires).Distinct(StringComparer.Ordinal).ToArray();

        internal static PackageAsset[] Write(VertexBuildContext context, string root)
        {
            var owned = (context.Job.Content ?? Array.Empty<BuildContent>()).Where(content => !content.External).ToArray();
            if (owned.Length == 0) return Array.Empty<PackageAsset>();
            if (owned.Length != 1) throw new InvalidDataException("A map package needs one owning content namespace.");
            var sourcePath = context.PathFor(owned[0].Path);
            var content = ContentManifest.Load(sourcePath);
            var targets = context.Job.Profile == "Release" ? context.Job.Game.Build.Targets.Select(target => target.Id).ToArray() : new[] { context.Job.Target };
            var nativeRoot = context.PathFor("VertexNativeCook");
            var previews = VertexNativeContent.Previews(owned[0].Path, nativeRoot);
            foreach (var preview in previews) content.Assets.Single(asset => asset.Id == preview.Id).Preview = preview.Preview;
            content.Platforms = VertexNativeContent.Build(owned[0].Path, nativeRoot, targets, context.CheckCanceled);
            var nativeFiles = content.Platforms.Select(platform => platform.Bundle).Concat(previews.Select(preview => preview.Preview.Path)).Distinct(StringComparer.Ordinal).Select(relative =>
            {
                var path = Path.Combine(nativeRoot, relative);
                if (!SdkFileHashUtility.TryComputeSha256(path, out var hash)) throw new IOException("Could not hash native content.");
                return new BuildFile { Path = relative, Bytes = new FileInfo(path).Length, Sha256 = hash };
            }).ToArray();
            foreach (var file in content.Files.Where(file => !nativeFiles.Any(compiled => compiled.Path == file.Path)))
            {
                context.CheckCanceled();
                var source = ProfileFiles.Resolve(sourcePath, file.Path, true);
                var destination = ProfileFiles.Resolve(Path.Combine(root, "addon.json"), file.Path, true);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, false);
                if (new FileInfo(destination).Length != file.Bytes ||
                    !SdkFileHashUtility.TryComputeSha256(destination, out var hash) || hash != file.Sha256)
                    throw new InvalidDataException("Captured media changed: " + file.Path);
            }
            foreach (var file in nativeFiles)
            {
                var destination = ProfileFiles.Resolve(Path.Combine(root, "addon.json"), file.Path, true);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(Path.Combine(nativeRoot, file.Path), destination, false);
            }
            content.Files = content.Files.Where(file => !nativeFiles.Any(compiled => compiled.Path == file.Path)).Concat(nativeFiles).ToArray();
            var stereoformAudio = SdkGame.Current.AudioRuntimeCapability == Vellocet.Sdk.Audio.AddonAudioBanks.Capability
                ? Vellocet.Sdk.Editor.Audio.StereoformBankBuild.BuildVertex(content, sourcePath, root) : null;
            var data = content.Serialize();
            File.WriteAllBytes(Path.Combine(root, ContentManifest.FileName), data);
            var manifestPath = Path.Combine(root, "addon.json");
            var manifest = JsonUtility.FromJson<AddonManifest>(File.ReadAllText(manifestPath));
            manifest.requires = (manifest.requires ?? Array.Empty<string>()).Concat(content.Requires).Distinct(StringComparer.Ordinal).ToArray();
            manifest.contents = manifest.contents.Append(AddonMedia.Entry(content, data)).ToArray();
            var audio = AddonMedia.AudioCatalog(content);
            if (stereoformAudio != null) manifest.contents = manifest.contents.Append(stereoformAudio).ToArray();
            if (audio.cues.Length > 0 && string.IsNullOrEmpty(SdkGame.Current.AudioRuntimeCapability))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(audio, true));
                File.WriteAllBytes(Path.Combine(root, "audio.json"), bytes);
                var sounds = audio.cues.Select(cue => cue.media).ToHashSet(StringComparer.Ordinal);
                manifest.contents = manifest.contents.Append(new AddonManifest.ContentEntry
                { kind = AddonManifest.AudioContentKind, id = "content_audio", title = content.Title,
                    audio = new AddonManifest.AudioContent { catalog = "audio.json", files = content.Files.Where(file => sounds.Contains(file.Path))
                        .Select(file => new AddonManifest.PlatformFile { path = file.Path, size = file.Bytes, sha256 = file.Sha256 })
                        .Append(new AddonManifest.PlatformFile { path = "audio.json", size = bytes.Length, sha256 = AssetCatalog.Hash(bytes) }).ToArray() } }).ToArray();
            }
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            AddonMedia.Read(root, manifest, manifest.contents.Single(entry => entry.kind == AddonManifest.MediaContentKind));
            var fingerprint = AssetCatalog.Hash(data);
            return content.Platforms.SelectMany(platform => VertexNativeContent.Entries(owned[0].Path).Select(entry => new PackageAsset
            {
                Id = VertexNativeContent.ReceiptId(entry), Source = UnityEditor.AssetDatabase.GetAssetPath(entry.asset),
                Ownership = "custom", Address = entry.key, Target = platform.Target, Fingerprint = fingerprint, Bundles = new[] { platform.Bundle }
            })).GroupBy(asset => (asset.Target, asset.Id)).Select(group => group.First()).ToArray();
        }
    }
}
