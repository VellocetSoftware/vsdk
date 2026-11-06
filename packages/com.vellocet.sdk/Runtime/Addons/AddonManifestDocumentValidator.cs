using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vellocet.Sdk.Addons
{
    public static class AddonManifestDocumentValidator
    {
        internal const long MaximumManifestBytes = AddonFilePolicy.MaximumManifestBytes;

        private static readonly ISet<string> RootProperties = Properties(
            "schemaVersion", "gameId", "audioRuntime", "id", "title", "author", "addonThumbnail", "version",
            "distributionProfile", "distributionPolicy", "buildId", "createdUtc", "sdkVersion",
            "unityVersion", "requires", "contents");

        private static readonly ISet<string> ContentProperties = Properties(
            "kind", "id", "title", "map", "mod", "audio", "media");

        // Accept legacy mode metadata when reading existing packages; the map prefix owns the mode.
        private static readonly ISet<string> MapProperties = Properties(
            "scene", "defaultModeId", "musicSet", "thumbnail", "compiledReceipt", "platforms");

        private static readonly ISet<string> ModProperties = Properties("manifest");
        private static readonly ISet<string> AudioProperties = Properties("catalog", "files");
        private static readonly ISet<string> PlatformProperties = Properties("name", "role", "catalog", "files");
        private static readonly ISet<string> FileProperties = Properties("path", "size", "sha256");

        public static bool TryValidate(string addonRoot, out string error)
        {
            error = null;
            var manifestPath = AddonHelper.GetManifestPath(addonRoot);
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                error = $"Addon manifest '{Constants.AddonManifestFileName}' is missing.";
                return false;
            }

            var info = new FileInfo(manifestPath);
            if (info.Length <= 0 || info.Length > MaximumManifestBytes)
            {
                error = $"Addon manifest must be between 1 and {MaximumManifestBytes} bytes.";
                return false;
            }

            try
            {
                using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    16 * 1024, FileOptions.SequentialScan);
                using var text = new StreamReader(stream);
                using var reader = new JsonTextReader(text)
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal,
                    MaxDepth = 32
                };
                var token = JToken.ReadFrom(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    LineInfoHandling = LineInfoHandling.Load,
                    CommentHandling = CommentHandling.Ignore
                });
                if (reader.Read())
                {
                    error = "Addon manifest contains more than one root JSON value.";
                    return false;
                }

                if (token is not JObject root)
                {
                    error = "Addon manifest root must be a JSON object.";
                    return false;
                }

                if (!ValidateObject(root, RootProperties, "addon manifest", out error))
                    return false;
                if (root["contents"] is not JArray contents)
                    return true;

                for (var index = 0; index < contents.Count; index++)
                {
                    if (contents[index] is not JObject content)
                    {
                        error = $"Addon manifest contents[{index}] must be an object.";
                        return false;
                    }

                    if (!ValidateObject(content, ContentProperties, $"contents[{index}]", out error) ||
                        !ValidateOptionalObject(content["media"], AudioProperties, $"contents[{index}].media",
                            out error) ||
                        !ValidateOptionalObject(content["map"], MapProperties, $"contents[{index}].map", out error) ||
                        !ValidateOptionalObject(content["mod"], ModProperties, $"contents[{index}].mod", out error) ||
                        !ValidateOptionalObject(content["audio"], AudioProperties, $"contents[{index}].audio",
                            out error))
                        return false;
                    if (!ValidatePlatformArray(content["map"]?["platforms"],
                            $"contents[{index}].map.platforms", out error) ||
                        !ValidateFileArray(content["media"]?["files"], $"contents[{index}].media.files", out error) ||
                        !ValidateFileArray(content["audio"]?["files"],
                            $"contents[{index}].audio.files", out error))
                        return false;
                }

                return true;
            }
            catch (JsonException ex)
            {
                error = $"Addon manifest JSON is invalid: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                error = $"Addon manifest could not be read: {ex.Message}";
                return false;
            }
        }

        private static bool ValidatePlatformArray(JToken token, string path, out string error)
        {
            error = null;
            if (token == null || token.Type == JTokenType.Null)
                return true;
            if (token is not JArray values)
            {
                error = $"{path} must be an array.";
                return false;
            }

            for (var index = 0; index < values.Count; index++)
                if (values[index] is not JObject value ||
                    !ValidateObject(value, PlatformProperties, $"{path}[{index}]", out error) ||
                    !ValidateFileArray(value["files"], $"{path}[{index}].files", out error))
                    return false;

            return true;
        }

        private static bool ValidateFileArray(JToken token, string path, out string error)
        {
            error = null;
            if (token == null || token.Type == JTokenType.Null)
                return true;
            if (token is not JArray values)
            {
                error = $"{path} must be an array.";
                return false;
            }

            for (var index = 0; index < values.Count; index++)
                if (values[index] is not JObject value ||
                    !ValidateObject(value, FileProperties, $"{path}[{index}]", out error))
                    return false;
            return true;
        }

        private static bool ValidateOptionalObject(JToken token, ISet<string> allowed, string path,
            out string error)
        {
            error = null;
            if (token == null || token.Type == JTokenType.Null)
                return true;
            if (token is JObject value)
                return ValidateObject(value, allowed, path, out error);
            error = $"{path} must be an object.";
            return false;
        }

        private static bool ValidateObject(JObject value, ISet<string> allowed, string path,
            out string error)
        {
            foreach (var property in value.Properties())
            {
                if (allowed.Contains(property.Name))
                    continue;
                error = $"{path} contains unsupported property '{property.Name}'.";
                return false;
            }

            error = null;
            return true;
        }

        private static ISet<string> Properties(params string[] values)
        {
            return new HashSet<string>(values ?? Array.Empty<string>(), StringComparer.Ordinal);
        }
    }
}