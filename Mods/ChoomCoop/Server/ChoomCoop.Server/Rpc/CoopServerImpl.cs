using ChoomCoopSystem;
using ChoomCoopSystem.Core;
using Cyberpunk.Rpc.Client.Protocol;

namespace Cyberpunk.Rpc.Server.Protocol
{
    public static partial class CoopServer
    {
        public static partial void Ping_Impl(ulong playerId, uint protocolVersion, uint nonce)
        {
            var reply = PingHandler.Handle(protocolVersion, nonce);

            if (reply.Accepted)
                Plugin.Log.Info($"[ChoomCoop:Net] Ping from player={playerId} nonce={nonce} protocol={protocolVersion} -> Pong");
            else
                Plugin.Log.Warn($"[ChoomCoop:Net] Ping from player={playerId} nonce={nonce} protocol mismatch client={protocolVersion} server={reply.ProtocolVersion} -> Pong(rejected)");

            CoopClient.Pong(playerId, reply.ProtocolVersion, reply.Nonce, reply.Accepted);
        }
    }
}
