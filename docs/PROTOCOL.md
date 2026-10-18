# Choom Co-op protocol

All Choom Co-op messages go over CyberpunkMP's RPC system (no second network stack).
How that system works: `docs/ARCHITECTURE_NOTES.md` §4.

- **Protocol version:** `1`
  - C#: `Mods/ChoomCoop/Shared/Protocol/ProtocolVersion.cs` (`ProtocolVersion.Current`)
  - Redscript: `ChoomCoopProtocol.Version()` in `Mods/ChoomCoop/Shared/Protocol/Protocol.reds`
  - A unit test fails if the two differ.
- **Rule:** every client -> server request carries `protocolVersion`. The server rejects
  mismatches and says so in the reply. Bump the version on any change to an RPC's
  arguments or meaning.
- **Redscript module:** `ChoomCoop.Protocol`. C# namespaces:
  `Cyberpunk.Rpc.Server.Protocol` / `Cyberpunk.Rpc.Client.Protocol`.
- **Ids:** FNV-1a 64 of the names below; constants in `Shared/Protocol/RpcIds.cs`.

## Messages

### `CoopServer.Ping` (client -> server)

| | |
|---|---|
| Since | v1 |
| Redscript | `CoopServer.Ping(protocolVersion: Uint32, nonce: Uint32)` |
| Class id | `hash("ChoomCoop.Protocol.CoopServer")` = 10360736183100188576 |
| Function id | `hash("Ping")` = 16843147157235268489 |
| Fields | `protocolVersion`: sender's protocol version. `nonce`: random number, echoed back. |
| Server | `CoopServer.Ping_Impl` -> `PingHandler.Handle` -> replies with `CoopClient.Pong` |
| Sent | 3 s after connecting (`NetworkWorldSystem.OnConnected` wrap) |

### `CoopClient.Pong` (server -> client)

| | |
|---|---|
| Since | v1 |
| Redscript | `CoopClient.Pong(protocolVersion: Uint32, nonce: Uint32, accepted: Bool)` |
| Class id | `hash("ChoomCoop.Protocol.CoopClient")` = 14946371136643445460 |
| Function id | `hash("Pong;Uint32Uint32Bool")` = 9876348574074121164 |
| Fields | `protocolVersion`: server's version. `nonce`: echoed from Ping. `accepted`: false on version mismatch. |
| Client | logs `[ChoomCoop:Net] Pong received ...` |
