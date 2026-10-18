using ChoomCoopSystem.Core;
using CyberpunkSdk;
using CyberpunkSdk.Systems;

namespace ChoomCoopSystem
{
    /// <summary>
    /// Discovered by the CyberpunkMP loader, which runs this static constructor once at startup.
    /// </summary>
    public class Plugin
    {
        public static Plugin Instance { get; }

        internal static readonly Logger Log = new("ChoomCoop");

        static Plugin()
        {
            Instance = new Plugin();
        }

        private Plugin()
        {
            Log.Info($"[ChoomCoop:Core] Server plugin loaded v{ModInfo.Version}");

            Server.PlayerSystem.PlayerJoinEvent += OnPlayerJoin;
            Server.PlayerSystem.PlayerLeftEvent += OnPlayerLeft;
        }

        private static void OnPlayerJoin(ulong id)
        {
            Log.Info($"[ChoomCoop:Session] Player joined id={id} name={TryGetName(id)}");
        }

        private static void OnPlayerLeft(ulong id)
        {
            Log.Info($"[ChoomCoop:Session] Player left id={id}");
        }

        private static string TryGetName(ulong id)
        {
            try
            {
                return Server.PlayerSystem.GetById(id).Username;
            }
            catch (Exception)
            {
                return "<unknown>";
            }
        }
    }
}
