// Logging helpers. Output goes through Codeware's ModLog (an engine log channel),
// which shows in the CET console. Every line is prefixed [ChoomCoop:<Subsystem>].
module ChoomCoop.Core

public abstract class ChoomCoopInfo {
    public static func ModVersion() -> String {
        return "0.0.1";
    }

    // Flip to true for verbose logs (CoopLogDebug).
    public static func IsDebug() -> Bool {
        return false;
    }
}

public static func CoopLog(subsystem: String, message: String) -> Void {
    ModLog(n"ChoomCoop", s"[ChoomCoop:\(subsystem)] \(message)");
}

public static func CoopLogError(subsystem: String, message: String) -> Void {
    ModLog(n"ChoomCoop", s"[ChoomCoop:\(subsystem)] ERROR \(message)");
}

public static func CoopLogDebug(subsystem: String, message: String) -> Void {
    if ChoomCoopInfo.IsDebug() {
        ModLog(n"ChoomCoop", s"[ChoomCoop:\(subsystem)] DEBUG \(message)");
    }
}
