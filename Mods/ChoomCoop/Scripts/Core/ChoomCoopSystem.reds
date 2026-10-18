// Entry point of the Choom Co-op client scripts.
module ChoomCoop.Core

import ChoomCoop.Protocol.*

// ScriptableSystems are created automatically when a game session starts.
public class ChoomCoopSystem extends ScriptableSystem {
    private let m_connectedListener: ref<CallbackHandle>;

    private func OnAttach() -> Void {
        CoopLog("Core", s"Client scripts loaded v\(ChoomCoopInfo.ModVersion()) protocol v\(ChoomCoopProtocol.Version())");

        // CyberpunkMP sets this flag in NetworkWorldSystem.OnConnected/OnDisconnected.
        // (We can't @wrapMethod that class: redscript can't wrap a class declared in another
        // mod's source.) fireIfValueExist covers a connection made before we attached.
        let blackboard = GameInstance.GetBlackboardSystem(this.GetGameInstance()).Get(GetAllBlackboardDefs().UIGameData);
        this.m_connectedListener = blackboard.RegisterListenerBool(GetAllBlackboardDefs().UIGameData.UIMultiplayerConnectedToServer, this, n"OnConnectedChanged", true);
    }

    private func OnDetach() -> Void {
        if IsDefined(this.m_connectedListener) {
            let blackboard = GameInstance.GetBlackboardSystem(this.GetGameInstance()).Get(GetAllBlackboardDefs().UIGameData);
            blackboard.UnregisterListenerBool(GetAllBlackboardDefs().UIGameData.UIMultiplayerConnectedToServer, this.m_connectedListener);
            this.m_connectedListener = null;
        }
    }

    // Ping a few seconds after connecting. RPC definitions already arrive before OnConnected
    // (code/server/native/GameServer.cpp HandleAuthentication); the delay just lets the
    // server finish creating our player first.
    protected cb func OnConnectedChanged(connected: Bool) -> Bool {
        if connected {
            CoopLog("Net", "Connected to server, Ping scheduled in 3s");
            GameInstance.GetDelaySystem(this.GetGameInstance()).DelayCallback(new ChoomCoopPingCallback(), 3.0, false);
        } else {
            CoopLog("Net", "Disconnected from server");
        }
        return true;
    }
}

public class ChoomCoopPingCallback extends DelayCallback {
    public func Call() -> Void {
        let nonce = Cast<Uint32>(RandRange(1, 1000000));
        CoopLog("Net", s"Ping sent nonce=\(nonce) protocol=\(ChoomCoopProtocol.Version())");
        CoopServer.Ping(ChoomCoopProtocol.Version(), nonce);
    }
}
