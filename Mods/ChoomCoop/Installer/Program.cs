// Choom Co-op dev-test installer.
//
// Guest: installs the pinned frameworks + CyberpunkMP (ChoomCoop build) + ChoomCoop scripts
// into the game, disables CET, and creates a "Play Choom Co-op" launcher pointing at the host.
// Host: the same, plus the server in %LOCALAPPDATA%\ChoomCoop\server and a start script.
//
// Never touches saves. Every file it would overwrite is backed up first, and
// `ChoomCoopSetup.exe --uninstall` removes the mod and turns CET back on.
//
// Options (all optional; anything missing is asked interactively):
//   --game <folder>     game folder (contains bin\x64\Cyberpunk2077.exe)
//   --role host|guest
//   --ip <address>      host IP for guests (host defaults to 127.0.0.1)
//   --port <n>          default 11778
//   --cache <folder>    use framework zips from this folder instead of downloading
//   --desktop <folder>  where to put the Play launcher (default: your Desktop)
//   --yes               accept defaults, no prompts
//   --uninstall

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ChoomCoopSetup;

internal record Framework(string Name, string Version, string Url, string Sha256);

internal static class Program
{
    private const string SetupVersion = "0.0.1";
    private const int DefaultPort = 11778;

    // Versions the Night City Online fork ships for game patch 2.31 (see CLAUDE.md).
    // Hashes recorded 2026-10-18 from the official GitHub release assets.
    private static readonly Framework[] Frameworks =
    [
        new("RED4ext", "1.29.1", "https://github.com/wopss/RED4ext/releases/download/v1.29.1/red4ext_1.29.1.zip",
            "d18fad7efa9bdfe054bd71952a5afcdcae4a11ce8d811c43267a8da6aa5bfe0b"),
        new("redscript", "0.5.31", "https://github.com/jac3km4/redscript/releases/download/v0.5.31/redscript-v0.5.31-windows.zip",
            "799bbd88863f6728616f8d723c941ea15dff71ed5bbfd50c525f0d39ea0bf46b"),
        new("Codeware", "1.18.0", "https://github.com/psiberx/cp2077-codeware/releases/download/v1.18.0/Codeware-1.18.0.zip",
            "7bb80ae80227312169f6c214b5f28ec18d1cd7b7b326cef7c365bcb0d9ebbb07"),
        new("ArchiveXL", "1.26.0", "https://github.com/psiberx/cp2077-archive-xl/releases/download/v1.26.0/ArchiveXL-1.26.0.zip",
            "5e301f85096d12525511e059c2f43910e4114320a8be9ebfa36774f17f9d79fe"),
        new("TweakXL", "1.11.1", "https://github.com/psiberx/cp2077-tweak-xl/releases/download/v1.11.1/TweakXL-1.11.1.zip",
            "d0436a454125aecb5d49d1de63a810972ac6596ec77d3cc12e1f28a443f8b6e7"),
        new("Input Loader", "0.2.3", "https://github.com/jackhumbert/cyberpunk2077-input-loader/releases/download/v0.2.3/input_loader_v0.2.3.zip",
            "6011515264bfb582591bf2ce66e956ac9c7993dfb5046590ba3ee7625af603ee"),
    ];

    private static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChoomCoop");
    private static readonly string StatePath = Path.Combine(DataDir, "install.json");
    private static StreamWriter? s_log;
    private static Dictionary<string, string> s_args = new();
    private static bool s_yes;
    private static string? s_serverLauncher;

    private static int Main(string[] argv)
    {
        Directory.CreateDirectory(DataDir);
        s_log = new StreamWriter(Path.Combine(DataDir, "setup.log"), append: true) { AutoFlush = true };
        ParseArgs(argv);

        try
        {
            Log($"=== ChoomCoopSetup v{SetupVersion} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            var code = s_args.ContainsKey("uninstall") ? Uninstall() : Install();
            Pause();
            return code;
        }
        catch (Exception e)
        {
            Log($"FAILED: {e.Message}");
            s_log?.WriteLine(e.ToString());
            Console.WriteLine();
            Console.WriteLine($"Setup failed. Send this file to Wyatt: {Path.Combine(DataDir, "setup.log")}");
            Pause();
            return 1;
        }
    }

    // ---------------------------------------------------------------- install

    private static int Install()
    {
        Console.WriteLine("Choom Co-op test build setup");
        Console.WriteLine("Installs: RED4ext, redscript, Codeware, ArchiveXL, TweakXL, Input Loader,");
        Console.WriteLine("CyberpunkMP (Choom Co-op build) and the Choom Co-op scripts. Disables CET.");
        Console.WriteLine("Your saves are not touched, but this is a TEST build: play on a test save.");
        Console.WriteLine();

        var game = FindGame();
        Log($"Game folder: {game}");
        var exe = Path.Combine(game, "bin", "x64", "Cyberpunk2077.exe");
        Log($"Game exe version: {FileVersionInfo.GetVersionInfo(exe).ProductVersion}");

        if (Process.GetProcessesByName("Cyberpunk2077").Length > 0)
            throw new InvalidOperationException("Cyberpunk 2077 is running. Close it and run setup again.");

        var role = Ask("role", "Are you the HOST (runs the server) or a GUEST? [host/guest]", "guest").ToLowerInvariant();
        if (role != "host" && role != "guest")
            throw new ArgumentException($"Unknown role '{role}'.");
        var ip = role == "host"
            ? Ask("ip", "IP the game should connect to", "127.0.0.1")
            : Ask("ip", "Host's IP address (Tailscale or public IP, ask the host)", "");
        if (string.IsNullOrWhiteSpace(ip))
            throw new ArgumentException("A host IP is required for guests.");
        var port = int.Parse(Ask("port", "Server port", DefaultPort.ToString()));

        var backupDir = Path.Combine(DataDir, "backup", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var state = LoadState() ?? new InstallState();
        state.Game = game;

        // 1. Frameworks
        foreach (var fw in Frameworks)
        {
            using var zip = GetFramework(fw);
            Log($"Installing {fw.Name} {fw.Version}");
            Extract(zip, null, game, backupDir);
        }

        // 2. CET off (its overlay plus CyberpunkMP's has hard-locked GPUs)
        var cet = Path.Combine(game, "bin", "x64", "version.dll");
        if (File.Exists(cet) && Directory.Exists(Path.Combine(game, "bin", "x64", "plugins", "cyber_engine_tweaks")))
        {
            var off = cet + ".choomcoop-off";
            if (File.Exists(off))
                File.Delete(off);
            File.Move(cet, off);
            state.CetDisabled = true;
            Log("Cyber Engine Tweaks disabled (version.dll renamed; uninstall restores it).");
        }

        // 3. Our mod. Remove the previous copy first so deleted files don't linger.
        using (var payload = OpenPayload())
        {
            var modDir = Path.Combine(game, "red4ext", "plugins", "zzzCyberpunkMP");
            var scriptsDir = Path.Combine(game, "r6", "scripts", "ChoomCoop");
            if (Directory.Exists(modDir)) Directory.Delete(modDir, true);
            if (Directory.Exists(scriptsDir)) Directory.Delete(scriptsDir, true);

            Log("Installing CyberpunkMP (Choom Co-op build) and Choom Co-op scripts");
            Extract(payload, "client/", game, backupDir);
            var buildInfo = payload.GetEntry("build.txt");
            if (buildInfo != null)
                Log("Build: " + new StreamReader(buildInfo.Open()).ReadToEnd().Trim());

            if (role == "host")
                InstallServer(payload, port);
        }

        // 4. Play launcher
        var desktop = s_args.GetValueOrDefault("desktop") ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var bat = Path.Combine(desktop, "Play Choom Co-op.bat");
        File.WriteAllText(bat,
            "@echo off\r\n" +
            "rem Created by ChoomCoopSetup. Starts Cyberpunk 2077 in multiplayer mode.\r\n" +
            $"start \"\" \"{exe}\" --online --ip={ip} --port={port}\r\n");
        state.PlayLauncher = bat;
        state.ServerLauncher = s_serverLauncher ?? state.ServerLauncher;
        Log($"Created {bat} (connects to {ip}:{port})");

        state.InstalledAt = DateTime.Now.ToString("s");
        SaveState(state);

        Console.WriteLine();
        Console.WriteLine("Done.");
        if (role == "host")
            Console.WriteLine("1. Start the server: double-click 'Start Choom Co-op Server.bat' on your Desktop.");
        Console.WriteLine($"{(role == "host" ? "2" : "1")}. Start the game with 'Play Choom Co-op.bat' on your Desktop (Steam must be running).");
        Console.WriteLine("Load a TEST save. Logs: <game>\\red4ext\\logs and <game>\\red4ext\\plugins\\zzzCyberpunkMP\\CyberpunkMP.log");
        Console.WriteLine($"Setup log: {Path.Combine(DataDir, "setup.log")}");
        return 0;
    }

    private static void InstallServer(ZipArchive payload, int port)
    {
        var serverDir = Path.Combine(DataDir, "server");
        Log($"Installing server to {serverDir}");
        // Keep config/ and logs/ between installs; replace everything else.
        if (Directory.Exists(serverDir))
        {
            foreach (var dir in Directory.GetDirectories(serverDir))
                if (Path.GetFileName(dir) is not ("config" or "logs"))
                    Directory.Delete(dir, true);
            foreach (var file in Directory.GetFiles(serverDir))
                File.Delete(file);
        }
        Extract(payload, "server/", serverDir, null);

        if (!HasDotnet8Runtime())
            Log("WARNING: the server needs the .NET 8 runtime (x64). Get it from https://dotnet.microsoft.com/download/dotnet/8.0 (\".NET Runtime\").");

        // The admin web panel requires credentials in release builds; local-only random values.
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var desktop = s_args.GetValueOrDefault("desktop") ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var bat = Path.Combine(desktop, "Start Choom Co-op Server.bat");
        File.WriteAllText(bat,
            "@echo off\r\n" +
            "rem Created by ChoomCoopSetup. Keep this window open while playing.\r\n" +
            $"rem Server port {port} (UDP). Allow it through Windows Firewall when asked.\r\n" +
            "set CYBERPUNKMP_ADMIN_USERNAME=admin\r\n" +
            $"set CYBERPUNKMP_ADMIN_PASSWORD={password}\r\n" +
            $"cd /d \"{serverDir}\"\r\n" +
            // Full path: with NoDefaultCurrentDirectoryInExePath set, cmd won't run a bare name from the cwd.
            $"\"{Path.Combine(serverDir, "Server.Loader.exe")}\"\r\n" +
            "pause\r\n");
        Log($"Created {bat}");
        s_serverLauncher = bat;
    }

    // ---------------------------------------------------------------- uninstall

    private static int Uninstall()
    {
        var state = LoadState();
        var game = state?.Game ?? FindGame();
        Log($"Uninstalling from {game}");

        foreach (var dir in new[] { Path.Combine(game, "red4ext", "plugins", "zzzCyberpunkMP"), Path.Combine(game, "r6", "scripts", "ChoomCoop") })
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
                Log($"Removed {dir}");
            }
        }

        var off = Path.Combine(game, "bin", "x64", "version.dll.choomcoop-off");
        if (File.Exists(off))
        {
            File.Move(off, Path.Combine(game, "bin", "x64", "version.dll"), overwrite: true);
            Log("Cyber Engine Tweaks re-enabled.");
        }

        foreach (var launcher in new[] { state?.PlayLauncher, state?.ServerLauncher })
            if (launcher != null && File.Exists(launcher))
                File.Delete(launcher);

        Log("The frameworks (RED4ext, redscript, Codeware, ArchiveXL, TweakXL, Input Loader) were left installed;");
        Log("other mods use them too. Files replaced during install are backed up in " + Path.Combine(DataDir, "backup"));
        if (File.Exists(StatePath))
            File.Delete(StatePath);
        Log("Uninstall complete.");
        return 0;
    }

    // ---------------------------------------------------------------- game discovery

    private static string FindGame()
    {
        if (s_args.TryGetValue("game", out var given))
            return ValidateGame(given);

        foreach (var candidate in SteamCandidates())
        {
            if (File.Exists(Path.Combine(candidate, "bin", "x64", "Cyberpunk2077.exe")))
            {
                var answer = Ask(null, $"Found Cyberpunk 2077 at {candidate}. Use it? [Y/n]", "y");
                if (!answer.StartsWith("n", StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }

        var typed = Ask(null, "Path to your Cyberpunk 2077 folder (the one containing 'bin')", "");
        return ValidateGame(typed.Trim('"'));
    }

    private static string ValidateGame(string folder)
    {
        var full = Path.GetFullPath(folder);
        if (!File.Exists(Path.Combine(full, "bin", "x64", "Cyberpunk2077.exe")))
            throw new DirectoryNotFoundException($"'{full}' doesn't contain bin\\x64\\Cyberpunk2077.exe.");
        return full;
    }

    private static IEnumerable<string> SteamCandidates()
    {
        var steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (steam == null)
            yield break;

        var libraries = new List<string> { steam };
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\""))
                libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }

        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            yield return Path.GetFullPath(Path.Combine(lib, "steamapps", "common", "Cyberpunk 2077"));
    }

    // ---------------------------------------------------------------- frameworks & payload

    private static ZipArchive GetFramework(Framework fw)
    {
        var fileName = Path.GetFileName(new Uri(fw.Url).LocalPath);
        string path;
        if (s_args.TryGetValue("cache", out var cache))
        {
            path = Path.Combine(cache, fileName);
        }
        else
        {
            path = Path.Combine(DataDir, "downloads", fileName);
            if (!File.Exists(path) || Sha256(path) != fw.Sha256)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                Log($"Downloading {fw.Name} {fw.Version} from {fw.Url}");
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"ChoomCoopSetup/{SetupVersion}");
                var bytes = http.GetByteArrayAsync(fw.Url).GetAwaiter().GetResult();
                File.WriteAllBytes(path, bytes);
            }
        }

        var hash = Sha256(path);
        if (hash != fw.Sha256)
            throw new InvalidDataException($"{fileName} has the wrong checksum ({hash}); refusing to install it.");
        return ZipFile.OpenRead(path);
    }

    private static ZipArchive OpenPayload()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
                     ?? throw new InvalidOperationException("This setup was built without the mod payload (run build.ps1).");
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    /// <summary>Extract entries (optionally only those under <paramref name="prefix"/>, with it stripped)
    /// into <paramref name="dest"/>, backing up any existing file first.</summary>
    private static void Extract(ZipArchive zip, string? prefix, string dest, string? backupDir)
    {
        {
            var root = Path.GetFullPath(dest) + Path.DirectorySeparatorChar;
            var count = 0;
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (prefix != null)
                {
                    if (!name.StartsWith(prefix, StringComparison.Ordinal))
                        continue;
                    name = name[prefix.Length..];
                }
                if (name.Length == 0)
                    continue;

                var target = Path.GetFullPath(Path.Combine(dest, name));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Archive entry escapes the target folder: {entry.FullName}");

                if (name.EndsWith('/'))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (backupDir != null && File.Exists(target))
                {
                    var backup = Path.Combine(backupDir, Path.GetRelativePath(dest, target));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(target, backup, overwrite: true);
                }
                entry.ExtractToFile(target, overwrite: true);
                count++;
            }
            s_log?.WriteLine($"  {count} files -> {dest}");
        }
    }

    // ---------------------------------------------------------------- helpers

    private static bool HasDotnet8Runtime()
    {
        var shared = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", "Microsoft.NETCore.App");
        return Directory.Exists(shared) && Directory.GetDirectories(shared, "8.*").Length > 0;
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void ParseArgs(string[] argv)
    {
        for (var i = 0; i < argv.Length; i++)
        {
            var key = argv[i].TrimStart('-').ToLowerInvariant();
            if (key is "yes" or "uninstall")
                s_args[key] = "true";
            else if (i + 1 < argv.Length)
                s_args[key] = argv[++i];
        }
        s_yes = s_args.ContainsKey("yes");
    }

    private static string Ask(string? argKey, string question, string defaultValue)
    {
        if (argKey != null && s_args.TryGetValue(argKey, out var given))
            return given;
        if (s_yes)
            return defaultValue;

        Console.Write(defaultValue.Length > 0 ? $"{question} (default {defaultValue}): " : $"{question}: ");
        var line = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(line) ? defaultValue : line;
    }

    private static void Log(string message)
    {
        Console.WriteLine(message);
        s_log?.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private static void Pause()
    {
        if (s_yes || Console.IsInputRedirected)
            return;
        Console.WriteLine();
        Console.Write("Press Enter to close.");
        Console.ReadLine();
    }

    private static InstallState? LoadState() =>
        File.Exists(StatePath) ? JsonSerializer.Deserialize(File.ReadAllText(StatePath), StateJson.Default.InstallState) : null;

    private static void SaveState(InstallState state) =>
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, StateJson.Default.InstallState));
}

internal class InstallState
{
    public string? Game { get; set; }
    public bool CetDisabled { get; set; }
    public string? PlayLauncher { get; set; }
    public string? ServerLauncher { get; set; }
    public string? InstalledAt { get; set; }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = true)]
[System.Text.Json.Serialization.JsonSerializable(typeof(InstallState))]
internal partial class StateJson : System.Text.Json.Serialization.JsonSerializerContext
{
}
