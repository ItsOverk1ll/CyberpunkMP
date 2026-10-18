namespace ChoomCoop.Shared.Protocol
{
    /// <summary>
    /// Wire protocol version. Bump on any change to an RPC signature or meaning.
    /// Must match ChoomCoopProtocol.Version() in Protocol.reds (enforced by a unit test).
    /// </summary>
    public static class ProtocolVersion
    {
        public const uint Current = 1;
    }
}
