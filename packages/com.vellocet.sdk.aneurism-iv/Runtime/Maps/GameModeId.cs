using System;

namespace Vellocet.Sdk.Aneurism
{
    public enum GameModeId
    {
        Nightmare,
        Crackdown,
        GunGame
    }

    public static class MapGameMode
    {
        public static readonly System.Collections.Generic.IReadOnlyList<ModeDescriptor> Descriptors =
            Array.AsReadOnly(new[]
            {
                new ModeDescriptor("nightmare", "Nightmare", "mode.nightmare", "nm"),
                new ModeDescriptor("crackdown", "Crackdown", "mode.crackdown", "cd"),
                new ModeDescriptor("gungame", "Gun Game", "mode.gungame", "gg")
            });

        public static bool TryResolve(string mapName, out GameModeId mode)
        {
            mode = default;
            if (string.IsNullOrWhiteSpace(mapName)) return false;
            for (var index = 0; index < Descriptors.Count; index++)
            {
                var prefix = Descriptors[index].MapIdPrefix + "_";
                if (!mapName.StartsWith(prefix, StringComparison.Ordinal) || mapName.Length <= prefix.Length) continue;
                mode = (GameModeId)index;
                return true;
            }
            return false;
        }

        public static string CanonicalId(GameModeId mode) => Descriptors[(int)mode].Id;

        public static GameModeId Resolve(string mapName) => TryResolve(mapName, out var mode) ? mode :
            throw new ArgumentException($"Map '{mapName}' needs a mode prefix: nm_, cd_, or gg_.", nameof(mapName));
    }
}
