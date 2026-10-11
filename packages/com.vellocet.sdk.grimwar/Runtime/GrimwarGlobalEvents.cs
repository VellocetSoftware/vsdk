namespace Vellocet.Sdk.VSig
{
    public static partial class VSigGlobalEvents
    {
        public static class Gravewar
        {
            public const string Source = "gravewar";
            public const string WaveStarted = "wave_started";
            public const string WaveEnded = "wave_ended";

            public static class WaveEndResults
            {
                public const string Cleared = "cleared";
                public const string SurvivorsDefeated = "survivors_defeated";
                public const string Cancelled = "cancelled";
            }
        }
    }
}
