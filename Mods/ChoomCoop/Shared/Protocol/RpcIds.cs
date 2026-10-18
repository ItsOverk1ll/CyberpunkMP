namespace ChoomCoop.Shared.Protocol
{
    /// <summary>
    /// RPC identifiers used by CyberpunkMP's RPC system.
    ///
    /// CyberpunkMP normally generates C# stubs by launching the game with --rpc. The ids it
    /// emits are FNV-1a 64 hashes of redscript names (verified against upstream's EmoteSystem):
    ///   class id           = hash("Module.ClassName")
    ///   server function id = hash("FunctionName")                  (native static funcs)
    ///   client function id = hash("FunctionName;ArgType1ArgType2") (redscript mangled name)
    /// The hand-written stubs use these constants; RpcIdTests recomputes every one.
    /// </summary>
    public static class RpcIds
    {
        public const string CoopServerClass = "ChoomCoop.Protocol.CoopServer";
        public const string CoopClientClass = "ChoomCoop.Protocol.CoopClient";
        public const string PingFunction = "Ping";
        public const string PongFunction = "Pong;Uint32Uint32Bool";

        public const ulong CoopServerClassId = 10360736183100188576;
        public const ulong PingId = 16843147157235268489;
        public const ulong CoopClientClassId = 14946371136643445460;
        public const ulong PongId = 9876348574074121164;

        public static ulong Fnv1a64(string text)
        {
            ulong hash = 0xcbf29ce484222325;
            foreach (var b in System.Text.Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 0x100000001b3;
            }
            return hash;
        }
    }
}
