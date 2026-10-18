using ChoomCoop.Shared.Protocol;

namespace ChoomCoopSystem.Core
{
    public readonly record struct PongReply(uint ProtocolVersion, uint Nonce, bool Accepted);

    public static class PingHandler
    {
        /// <summary>Echo the nonce; accept only clients on our protocol version.</summary>
        public static PongReply Handle(uint clientProtocolVersion, uint nonce)
        {
            var accepted = clientProtocolVersion == ProtocolVersion.Current;
            return new PongReply(ProtocolVersion.Current, nonce, accepted);
        }
    }
}
