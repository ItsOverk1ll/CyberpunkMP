// Choom Co-op wire protocol (client side).
// Every RPC here is mirrored by hand-written C# stubs in
// Mods/ChoomCoop/Server/ChoomCoop.Server/Rpc/ and documented in docs/PROTOCOL.md.
// Keep ChoomCoopProtocol.Version() in sync with Shared/Protocol/ProtocolVersion.cs
// (a unit test enforces this).
module ChoomCoop.Protocol

import CyberpunkMP.*
import ChoomCoop.Core.*

public abstract class ChoomCoopProtocol {
    public static func Version() -> Uint32 {
        return 1u;
    }
}

// Client -> Server. Implemented on the server by CoopServer.Ping_Impl.
public native class CoopServer extends ServerRpc {
    public static native func Ping(protocolVersion: Uint32, nonce: Uint32) -> Void;
    // Sent when a Pong arrives, so the server log proves the full round trip
    // (the client console may not be available: CET is off during MP tests).
    public static native func PongAck(protocolVersion: Uint32, nonce: Uint32) -> Void;
}

// Server -> Client. CyberpunkMP invokes these with no instance: never touch `this`.
public class CoopClient extends ClientRpc {
    public func Pong(protocolVersion: Uint32, nonce: Uint32, accepted: Bool) -> Void {
        if accepted {
            CoopLog("Net", s"Pong received nonce=\(nonce) serverProtocol=\(protocolVersion)");
            CoopServer.PongAck(ChoomCoopProtocol.Version(), nonce);
        } else {
            CoopLogError("Net", s"Pong received nonce=\(nonce) but server REJECTED protocol v\(ChoomCoopProtocol.Version()) (server speaks v\(protocolVersion))");
        }
    }
}
