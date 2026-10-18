// Entry point of the Choom Co-op client scripts.
module ChoomCoop.Core

import CyberpunkMP.World.*
import ChoomCoop.Protocol.*

// ScriptableSystems are created automatically when a game session starts.
public class ChoomCoopSystem extends ScriptableSystem {
    private func OnAttach() -> Void {
        CoopLog("Core", s"Client scripts loaded v\(ChoomCoopInfo.ModVersion()) protocol v\(ChoomCoopProtocol.Version())");
    }
}

// Send a Ping a few seconds after connecting. RPC definitions already arrive before
// OnConnected (code/server/native/GameServer.cpp HandleAuthentication); the delay just
// lets the server finish creating our player first.
@wrapMethod(NetworkWorldSystem)
public func OnConnected() -> Void {
    wrappedMethod();
    CoopLog("Net", "Connected to server, Ping scheduled in 3s");
    GameInstance.GetDelaySystem(GetGameInstance()).DelayCallback(new ChoomCoopPingCallback(), 3.0, false);
}

public class ChoomCoopPingCallback extends DelayCallback {
    public func Call() -> Void {
        let nonce = Cast<Uint32>(RandRange(1, 1000000));
        CoopLog("Net", s"Ping sent nonce=\(nonce) protocol=\(ChoomCoopProtocol.Version())");
        CoopServer.Ping(ChoomCoopProtocol.Version(), nonce);
    }
}
