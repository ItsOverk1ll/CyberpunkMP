using CyberpunkSdk.Systems;

namespace ChoomCoopSystem
{
    /// <summary>
    /// Logs to the server console (SDK logger) and to logs/ChoomCoop-session.log, flushed on
    /// every line. The SDK's per-plugin file is only written on a clean shutdown, so closing
    /// the server window lost every ChoomCoop line during testing.
    /// </summary>
    internal sealed class CoopLog
    {
        private readonly Logger _sdk = new("ChoomCoop");
        private readonly StreamWriter? _file;
        private readonly object _lock = new();

        public CoopLog()
        {
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                _file = new StreamWriter(Path.Combine(dir, "ChoomCoop-session.log"), append: true) { AutoFlush = true };
                _file.WriteLine($"==== server start {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception e)
            {
                _sdk.Warn($"[ChoomCoop:Core] could not open ChoomCoop-session.log: {e.Message}");
            }
        }

        public void Info(string message)
        {
            _sdk.Info(message);
            Write("info", message);
        }

        public void Warn(string message)
        {
            _sdk.Warn(message);
            Write("warn", message);
        }

        private void Write(string level, string message)
        {
            if (_file == null)
                return;
            lock (_lock)
                _file.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}");
        }
    }
}
