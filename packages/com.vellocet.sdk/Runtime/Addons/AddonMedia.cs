using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Vertex.Contracts;
using Vellocet.Sdk.Audio;

namespace Vellocet.Sdk.Addons
{
    /// <summary>The SDK owns media formats and playback policy; authoring tools display this data unchanged.</summary>
    public static class AddonMedia
    {
        public const string SharedAudioType = "vellocet.audio";
        public const string LegacyGrimwarAudioType = "grimwar.audio";
        public static string AudioType => SdkGame.Current.AudioContentTypeId;
        public static SdkContentType[] Types() => new[]
        {
            new SdkContentType { Id = AudioType, Name = string.IsNullOrEmpty(SdkGame.Current.AudioRuntimeCapability) ? "Sound" : "Audio event", Kind = string.IsNullOrEmpty(SdkGame.Current.AudioRuntimeCapability) ? "data" : "audio", Extensions = string.IsNullOrEmpty(SdkGame.Current.AudioRuntimeCapability) ? new[] { ".wav", ".ogg", ".mp3" } : new[] { ".wav", ".ogg", ".mp3", ".sfevent" },
                MaximumBytes = AddonAudioPolicy.MaximumSourceFileBytes, Settings = new[]
                {
                    new EntityPropertyDefinition { Id = "template", Name = "Playback", Type = "enum", DefaultValue = 2,
                        Choices = Enum.GetValues(typeof(AddonAudioTemplate)).Cast<AddonAudioTemplate>().Select(value => new EntityPropertyChoice
                        { Id = AddonAudioPolicy.ToCatalogValue(value), Name = value.ToString(), Value = (int)value }).ToArray() },
                    new EntityPropertyDefinition { Id = "gain", Name = "Volume", Type = "number", DefaultValue = 1d, Minimum = 0, Maximum = 1 }
                } },
            new SdkContentType { Id = "unity.material", Name = "Surface material", Kind = "material", MaximumBytes = AddonFilePolicy.MaximumImageBytes,
                Extensions = new[] { ".png", ".jpg", ".jpeg", ".tga" } },
            new SdkContentType { Id = "unity.texture", Name = "Image / texture", Kind = "texture", MaximumBytes = AddonFilePolicy.MaximumImageBytes,
                Extensions = new[] { ".png", ".jpg", ".jpeg", ".tga" } },
            new SdkContentType { Id = "unity.model", Name = "Model", Kind = "model", Extensions = new[] { ".fbx", ".obj" },
                CompanionExtensions = new[] { ".mtl", ".png", ".jpg", ".jpeg", ".tga" } },
            new SdkContentType { Id = "unity.font", Name = "Font", Kind = "font", Extensions = new[] { ".ttf", ".otf" } },
            new SdkContentType { Id = "unity.video", Name = "Video", Kind = "video", Extensions = new[] { ".mp4", ".webm", ".mov" } },
            new SdkContentType { Id = "file.text", Name = "Text / data", Kind = "text", MaximumBytes = AddonFilePolicy.MaximumTextFileBytes,
                Extensions = new[] { ".txt", ".json", ".csv", ".md" } },
            new SdkContentType { Id = "file.binary", Name = "Binary data", Kind = "data", Extensions = new[] { ".bin" } }
        };

        public static AddonAudioCatalog AudioCatalog(ContentManifest content)
        {
            var type = Types().Single(value => value.Id == AudioType);
            var assets = content.Assets.Where(asset => asset.Type == AudioType).ToArray();
            if (assets.Length > AddonAudioPolicy.MaximumCueCount) throw new InvalidDataException("Too many sounds in this content package.");
            return new AddonAudioCatalog { cues = assets.Select(asset =>
            {
                type.Validate(asset);
                return new AddonAudioCatalog.Cue { id = content.Key(asset), media = asset.Source,
                    template = AddonAudioPolicy.ToCatalogValue((AddonAudioTemplate)int.Parse(asset.Setting("template", "2"), CultureInfo.InvariantCulture)),
                    gain = float.Parse(asset.Setting("gain", "1"), CultureInfo.InvariantCulture) };
            }).ToArray() };
        }

        public static AddonManifest.ContentEntry Entry(ContentManifest content, byte[] manifestBytes) => new AddonManifest.ContentEntry
        {
            kind = AddonManifest.MediaContentKind, id = "media", title = content.Title,
            media = new AddonManifest.MediaContent { catalog = ContentManifest.FileName,
                files = content.Files.Select(file => new AddonManifest.PlatformFile { path = file.Path, size = file.Bytes, sha256 = file.Sha256 })
                    .Append(new AddonManifest.PlatformFile { path = ContentManifest.FileName, size = manifestBytes.Length, sha256 = AssetCatalog.Hash(manifestBytes) }).ToArray() }
        };

        public static ContentManifest Read(string root, AddonManifest manifest, AddonManifest.ContentEntry entry,
            Func<string, long, string, bool> verify = null)
        {
            if (entry?.media?.catalog != ContentManifest.FileName || entry.media.files == null || entry.media.files.Length > 16385)
                throw new InvalidDataException("Media content needs a root content.json and a bounded file inventory.");
            var files = new Dictionary<string, AddonManifest.PlatformFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in entry.media.files)
            {
                if (file == null || !AddonFilePolicy.TryValidateFileSize(file.path, file.size, out var error) ||
                    !AddonFilePolicy.IsValidSha256(file.sha256) || !files.TryAdd(file.path, file))
                    throw new InvalidDataException("Invalid media file inventory.");
                if (verify != null)
                {
                    if (!verify(file.path, file.size, file.sha256)) throw new InvalidDataException("Media file failed verification: " + file.path);
                }
                else
                {
                    var path = ProfileFiles.Resolve(Path.Combine(root, "addon.json"), file.path, true);
                    using var stream = File.OpenRead(path);
                    using var sha = SHA256.Create();
                    if (stream.Length != file.size || BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != file.sha256)
                        throw new InvalidDataException("Media file changed: " + file.path);
                }
            }
            if (!files.ContainsKey(ContentManifest.FileName)) throw new InvalidDataException("The media catalog is not inventoried.");
            var content = ReadCatalog(root, entry);
            if (content.Id != manifest.id || content.Files == null || content.Files.Length + 1 != files.Count ||
                content.Requires.Except(manifest.requires ?? Array.Empty<string>(), StringComparer.Ordinal).Any())
                throw new InvalidDataException("Content identity or requirements disagree with the add-on manifest.");
            foreach (var file in content.Files)
                if (!files.TryGetValue(file.Path, out var declared) || declared.size != file.Bytes || declared.sha256 != file.Sha256)
                    throw new InvalidDataException("Content inventories disagree: " + file.Path);
            var types = Types().ToDictionary(type => type.Id);
            if (content.Assets.Any(asset => asset.Type.StartsWith("unity.", StringComparison.Ordinal)))
            {
                if (content.Platforms == null || content.Platforms.Length == 0 || content.Platforms.Any(platform => !AddonDistributionPolicy.ReleasePlatformNames.Contains(platform.Target)))
                    throw new InvalidDataException("Native media must be compiled for a supported platform.");
                if (manifest.distributionProfile == AddonDistributionPolicy.ReleaseProfile &&
                    AddonDistributionPolicy.ReleasePlatformNames.Except(content.Platforms.Select(platform => platform.Target)).Any())
                    throw new InvalidDataException("Release media must include every supported platform.");
            }
            foreach (var asset in content.Assets)
            {
                if (!types.TryGetValue(asset.Type, out var type)) throw new InvalidDataException("Unsupported media type: " + asset.Type);
                type.Validate(asset);
                if (content.Files.Single(file => file.Path == asset.Source).Bytes > type.MaximumBytes)
                    throw new InvalidDataException("Media exceeds the SDK type limit: " + asset.Source);
                foreach (var key in asset.Dependencies.Select(key => ContentManifest.ResolveKey(content.Id, key)))
                {
                    AssetCatalog.TryParseKey(key, out var owner, out var id);
                    if (owner == content.Id ? !content.Assets.Any(candidate => candidate.Id == id) : !content.Requires.Contains(owner))
                        throw new InvalidDataException("Undeclared media dependency: " + key);
                }
            }
            return content;
        }

        public static ContentManifest ReadCatalog(string root, AddonManifest.ContentEntry entry)
        {
            var declared = entry?.media?.files?.SingleOrDefault(file => file?.path == ContentManifest.FileName)
                ?? throw new InvalidDataException("Media catalog is not inventoried.");
            var bytes = ProfileFiles.Read(ProfileFiles.Resolve(Path.Combine(root, "addon.json"), ContentManifest.FileName, true), ContentManifest.MaximumManifestBytes);
            if (bytes.LongLength != declared.size || AssetCatalog.Hash(bytes) != declared.sha256)
                throw new InvalidDataException("Media catalog changed while loading.");
            return ContentManifest.Deserialize(bytes);
        }
    }
}
