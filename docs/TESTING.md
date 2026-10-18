# Testing

Manual in-game test scripts. Record pass/fail per build at the bottom of each section.
Claude cannot run the game: everything here is for Wyatt.

Use **throwaway test saves**, never your real playthrough.

---

## Where the logs are

| What | Where |
|---|---|
| RED4ext (did plugins load?) | `<game>/red4ext/logs/red4ext-*.log` |
| CyberpunkMP client | `<game>/red4ext/plugins/zzzCyberpunkMP/CyberpunkMP.log` |
| redscript compile errors | `<game>/r6/logs/redscript_rCURRENT.log` (+ a popup if compile fails) |
| ChoomCoop client lines | CET console (overlay key, default `~`), lines starting `[ChoomCoop:` |
| CET console log file | `<game>/bin/x64/plugins/cyber_engine_tweaks/cyber_engine_tweaks.log` and `scripting.log` |
| Server | the server console window, plus `<server>/logs/Server.log` and `<server>/logs/ChoomCoop-session.log` (always flushed) |

`<game>` = your Cyberpunk 2077 folder (contains `bin/`, `r6/`, `red4ext/`).
`<server>` = `P:\CyberpunkMP\distrib\launcher\server` (or `build\windows\x64\releasedbg`).

Plugin-specific server log files (`logs/ChoomCoop.log`) may stay empty until the server
exits cleanly; use `ChoomCoop-session.log` instead.

The server prints `Server could not reach the server list!` every minute. That's the
public CyberpunkMP server list, offline since upstream stopped; it doesn't affect direct
connections. Ignore it.

**Connecting in game:** after loading a save, hold **`/`** (the "Connect to server" input hint) for half
a second, **once**. `/` is also **disconnect** while connected (same key,
upstream binding), so don't hold it twice. After a world reload (loading a save, the
prologue ending) the on-screen hint may say "Connect" again while you're still connected;
hold `/` once and it re-syncs instead of reconnecting.

---

## Quick setup: ChoomCoopSetup.exe (recommended for testers)

One exe installs everything for a tester. Build it on the dev PC:

```
xmake -y -j 4
xmake install -y -o distrib
.\Mods\ChoomCoop\Installer\build.ps1
```

Output: `Mods\ChoomCoop\Installer\dist\ChoomCoopSetup.exe` (about 20 MB). It contains our
CyberpunkMP build, the ChoomCoop scripts and the server. It downloads the six frameworks
(table below) from their official GitHub releases and checks each SHA-256 before installing.

What it does (game closed, Steam game folder found automatically, or it asks for the path):
1. Installs RED4ext, redscript, Codeware, ArchiveXL, TweakXL and Input Loader at the pinned
   versions. Any file it overwrites is backed up to `%LOCALAPPDATA%\ChoomCoop\backup\`.
2. Disables CET if present (`version.dll` -> `version.dll.choomcoop-off`).
3. Installs `red4ext\plugins\zzzCyberpunkMP\` and `r6\scripts\ChoomCoop\`.
4. Asks **host or guest**. Guests enter the host's IP. Creates `Play Choom Co-op.bat` on the
   Desktop (`--online --ip=<host> --port=11778`).
5. Host only: installs the server to `%LOCALAPPDATA%\ChoomCoop\server\` and creates
   `Start Choom Co-op Server.bat` on the Desktop. The server needs the .NET 8 runtime.

It never touches saves. `ChoomCoopSetup.exe --uninstall` removes the mod and both
launchers and turns CET back on (the frameworks stay; other mods use them). Log:
`%LOCALAPPDATA%\ChoomCoop\setup.log`.

The exe is unsigned, so Windows SmartScreen will warn ("More info" -> "Run anyway") and
some antivirus may flag it.

**Connecting over the internet.** The friend's game must reach the host's UDP port 11778.
Easiest: both install [Tailscale](https://tailscale.com), join the same tailnet, and the
guest uses the host's `100.x.y.z` Tailscale IP. Alternative: forward UDP 11778 on the
host's router to the host PC and give the guest your public IP. Either way, allow
`Server.Loader.exe` through Windows Firewall when asked, for **private and public**
networks (Tailscale can show up as either). Windows blocks ping by default, so `ping`
failing proves nothing; use `tailscale ping <host>`.

**Steam launch.** The Play launcher starts `Cyberpunk2077.exe` directly; Steam must be
running. If the game ignores the flags, put `--online --ip=<host> --port=11778` in Steam ->
Cyberpunk 2077 -> Properties -> Launch Options instead and start it from Steam.

## How to install the dev build into the game (manual)

**Prerequisites (once, both PCs).** Game patch **2.31** plus exactly these versions, from
each project's GitHub releases page (links in `README.md`; Input Loader is
`jackhumbert/cyberpunk2077-input-loader`):

| Framework | Version |
|---|---|
| RED4ext | 1.29.1 |
| redscript | 0.5.31 |
| Codeware | 1.18.0 |
| ArchiveXL | 1.26.0 |
| TweakXL | 1.11.1 |
| Input Loader | 0.2.3 |

Install each per its instructions, start the game once single-player to confirm it runs.

> **CET must be OFF for multiplayer sessions.** CET's overlay plus CyberpunkMP's overlay
> have hard-locked GPUs. Disable it by renaming `<game>\bin\x64\version.dll` to
> `version.dll.off` (rename back to re-enable). CET is only for the probe sessions below.

> Use dedicated test saves. If you have other mods installed, move them out for testing.

**Build (dev PC):**

```
xmake -y -j 4
xmake install -y -o distrib
```

(`xmake install` ends with an Electron launcher packaging error; ignore it, the mod and
server are installed before that step. Check that `distrib\launcher\mod\CyberpunkMP.dll`
has a fresh timestamp.)

**Install (each game PC), game closed:**

1. Copy `distrib\launcher\mod\*` (the DLL and `assets\`) into
   `<game>\red4ext\plugins\zzzCyberpunkMP\`. Copy, don't symlink: the plugin looks for
   `assets\redscript` beside its own path.
2. Copy `distrib\choomcoop\r6\scripts\ChoomCoop\` into `<game>\r6\scripts\ChoomCoop\`.
3. Probes only (separate session, see the end of this file): copy
   `Mods\ChoomCoop\CET\ChoomCoopProbes\` into
   `<game>\bin\x64\plugins\cyber_engine_tweaks\mods\ChoomCoopProbes\`.

**Server (host PC only):** run `distrib\launcher\server\Server.Loader.exe` from a normal
terminal (it needs a real console window). Release builds need two environment variables
for the admin web panel; any values work locally:

```
$env:CYBERPUNKMP_ADMIN_USERNAME = 'devadmin'; $env:CYBERPUNKMP_ADMIN_PASSWORD = 'pick-anything'; .\Server.Loader.exe
```

Allow it through Windows Firewall when asked (port **11778**, private network).

**Launch the game online:** add launch options (Steam: Properties -> Launch Options;
GOG Galaxy: Manage installation -> Configure -> Arguments; or run
`<game>\bin\x64\Cyberpunk2077.exe` from a shortcut). Use the `=` form:

```
--online --ip=127.0.0.1 --port=11778
```

On the guest PC use the host's LAN (or Tailscale) IP instead of 127.0.0.1.

---

## Phase 0 in-game test

Goal: two players in Night City on the fork, both see each other; ChoomCoop server and
client logs appear; Ping/Pong round-trips.

Setup: host PC runs the server and game A; guest PC runs game B. (Two instances on one PC
is unverified for this game; prefer two PCs.)

1. **Server starts.** On the host, start the server (see above). In its console, check:
   - [ ] `[Plugin:ChoomCoop] [ChoomCoop:Core] Server plugin loaded v0.0.1`
   - [ ] `[Plugin:SDK] Loaded Plugin: ChoomCoopSystem`
   (Already verified headless on the dev PC on 2026-10-18.)
2. **Host joins.** CET off. Start game A with `--online --ip=127.0.0.1`. Load the host
   test save.
   - [ ] The game reaches the main menu (if it closes or errors at startup, send
     `red4ext/logs/red4ext-*.log`).
   - [ ] `red4ext/logs/red4ext-*.log` shows CyberpunkMP loaded (no "failed to load").
   - [ ] No redscript compile-error popup. If one appears, copy its text and
     `r6/logs/redscript_rCURRENT.log`, then stop.
   - [ ] Server console: `[ChoomCoop:Session] Player joined id=<n> name=<name>`
3. **Ping/Pong round trip (host).** About 3 seconds after connecting, in the **server
   console** (this proves the client scripts loaded and both RPC directions work):
   - [ ] `[ChoomCoop:Net] Ping from player=<id> nonce=<N> protocol=1 -> Pong`
   - [ ] `[ChoomCoop:Net] PongAck from player=<id> nonce=<N> protocol=1 -> round trip OK` (same N)
   (The client also logs `[ChoomCoop:Core] Client scripts loaded`, `Ping sent` and
   `Pong received` lines, but only CET shows them, and CET is off.)
4. **Guest joins.** Start game B with `--online --ip=<host IP>`, load the guest test save.
   - [ ] Server: second `Player joined` line, then Ping and PongAck lines for the guest's id.
5. **See each other.** Walk both characters to the same spot (e.g. both fast travel to
   the same point, or load saves in the same district; players far apart are not synced).
   - [ ] Host sees the guest's character move; guest sees the host.
   - [ ] Appearance roughly matches (body, clothes).
6. **Vehicle (tests our threading fix).** Host gets in a car and drives 100 m; guest
   watches. Then guest gets in as passenger if possible. Host exits.
   - [ ] No crash on either PC; the guest sees the car move with the host in it.
7. **Leave.** Guest quits to the main menu.
   - [ ] Server: `[ChoomCoop:Session] Player left id=<n>`; host no longer sees the guest.
8. **RPC stub check (once, either PC, server not needed).** Launch the game with
   `--online --rpc --rpcdir="C:\temp\cpmp-rpc"`. It exits by itself after loading.
   - [ ] Send me `C:\temp\cpmp-rpc\Server\Protocol.CoopServer.cs` and
     `C:\temp\cpmp-rpc\Client\Protocol.CoopClient.cs`. I'll compare their ids with
     `Shared/Protocol/RpcIds.cs`. (If the folder is empty, send `CyberpunkMP.log`.)

**What to send back:** pass/fail per checkbox, the whole server console text, and any
error popups. Always also `red4ext-*.log` and `CyberpunkMP.log` from both PCs (they show
spawn/appearance/vehicle lines), plus `redscript_rCURRENT.log` if scripts misbehave. If a
game crashes, note roughly what was happening (spawn, car, shooting) and the time.

Results:
| Date | Build (commit) | Game version | Result | Notes |
|---|---|---|---|---|
| | | | | |

---

## Phase 0 research probes

These answer engine questions that can't be settled without running the game. They need
CET but not multiplayer, so run them in a **separate single-player session with
CyberpunkMP removed** (move `<game>\red4ext\plugins\zzzCyberpunkMP\` and
`<game>\r6\scripts\ChoomCoop\` out temporarily) and CET enabled. Install the probe CET mod
(step 3 of the install), load a test save, open the CET overlay and press the buttons. Send back every
`[ChoomCoop:Probe]` line plus what you saw. Probe code:
`Mods/ChoomCoop/CET/ChoomCoopProbes/init.lua`.

| Probe | Button | Answers | Do this |
|---|---|---|---|
| P1 | teleport 560m + streaming wait | Does `IsNavmeshStreamedInLocation` turn true after a teleport, and how long does it take? | Stand outdoors in a city street. Press, watch the log for ~15 s. Note if you fell or landed on a roof. Try twice in different districts. |
| P2 | navmesh point near me | Does `FindPointInSphereOnlyHumanNavmesh` return OK (status 0) and a sensible point? | Press on a street, then indoors (an apartment), then on a rooftop. |
| P3 | add / remove save lock | Does a script save lock block manual save, quicksave and autosave? | Press add. Try pause-menu Save (greyed?), F5 quicksave (message?), then fast travel (autosave appears in the load menu?). Press remove. |
| P4 | print scene tier | What HighLevel/SceneTier values do free roam, a dialogue, a car ride and sitting give? | Press in free roam; then during a dialogue scene; in a car as driver and as passenger. |
| P5 | spawn muppet puppet | Does a `Character.MaMuppet` spawn, stand still, and ignore you? | Press, look 2 m east, walk around it, shoot near it. |
| P6 | kill looked-at NPC | Does setting Health=0 kill an NPC through the normal death path? | Aim at a hostile gang member, press. Ragdoll? Loot? Did you get XP? |
| P7 | on-screen notification | Does the OnscreenMessage blackboard show text? | Press. |
| P8 | read quest fact | Do quest-fact reads work from script? | Press. |

Probe results:
| Date | Game version | Probe | Result |
|---|---|---|---|
| | | | |
