namespace ChoomCoopSystem.Core
{
    // Everything under Core/ is plain C# with no CyberpunkSdk dependency, so the test
    // project can compile it directly and run without the game or the native server.
    public static class ModInfo
    {
        public const string Version = "0.0.1";
    }
}
