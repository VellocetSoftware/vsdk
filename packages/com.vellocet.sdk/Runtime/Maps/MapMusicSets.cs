using System;
using System.IO;

namespace Vellocet.Sdk.Maps
{
    public static class MapMusicSets
    {
        public static MapMusicSet Default => SdkGame.Current.DefaultMusicSet;

        public static bool IsValid(MapMusicSet value) =>
            Enum.IsDefined(typeof(MapMusicSet), value);

        public static MapMusicSet Resolve(MapMusicSet value) => value == MapMusicSet.Unassigned ? Default :
            IsValid(value) ? value : throw new InvalidDataException($"Unknown map music set '{value}'.");

        public static string Name(MapMusicSet value) => Resolve(value).ToString();

        public static bool TryParse(string value, out MapMusicSet musicSet)
        {
            musicSet = Default;
            if (string.IsNullOrWhiteSpace(value)) return true;
            if (!Enum.TryParse(value, out musicSet) || !IsValid(musicSet) || musicSet.ToString() != value)
                return false;
            musicSet = Resolve(musicSet);
            return true;
        }

        public static MapMusicSet Parse(string value) => TryParse(value, out var musicSet) ? musicSet :
            throw new InvalidDataException($"Unknown map music set '{value}'.");
    }
}
