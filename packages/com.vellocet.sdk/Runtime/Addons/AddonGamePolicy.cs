using System;
using System.IO;
using System.Linq;

namespace Vellocet.Sdk.Addons
{
    public static class AddonGamePolicy
    {
        public static bool TryValidate(AddonManifest manifest, SdkGameDefinition game, out string error)
        {
            if (manifest == null || game == null)
                throw new ArgumentNullException(manifest == null ? nameof(manifest) : nameof(game));
            error = null;
            if (!string.Equals(manifest.gameId, game.Id, StringComparison.Ordinal) &&
                (game.RequiresPackageGameId || !string.IsNullOrEmpty(manifest.gameId)))
                error = $"This package targets '{manifest.gameId ?? "an unspecified game"}', not {game.DisplayName}.";
            else if (!string.IsNullOrEmpty(game.AudioRuntimeCapability) && manifest.audioRuntime != game.AudioRuntimeCapability)
                error = $"This content uses an incompatible audio runtime. Rebuild it with the current {game.DisplayName} SDK ({game.AudioRuntimeCapability}).";
            else if (!game.SupportsExternalMods && (manifest.contents ?? Array.Empty<AddonManifest.ContentEntry>())
                         .Any(entry => entry != null && (entry.mod != null ||
                             string.Equals(entry.kind, AddonManifest.ModContentKind, StringComparison.OrdinalIgnoreCase))))
                error = $"{game.DisplayName} supports maps and VSig; external code mods are not supported.";
            return error == null;
        }

        public static bool IsCodeFile(string path) => Path.GetExtension(path).ToLowerInvariant() is
            ".dll" or ".exe" or ".cs" or ".so" or ".dylib" or ".vmod";
    }
}
