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
| Server | the server console window, plus `<server>/logs/Server.log` |

`<game>` = your Cyberpunk 2077 folder (contains `bin/`, `r6/`, `red4ext/`).
`<server>` = `P:\CyberpunkMP\distrib\launcher\server` (or `build\windows\x64\releasedbg`).

Plugin-specific server log files (`logs/ChoomCoop.log`) may stay empty until the server
exits cleanly; the server console always shows `[Plugin:ChoomCoop]` lines.

---

## How to install the dev build into the game

**Prerequisites (once, both PCs).** Game version matching the build (see the warning
below), plus these frameworks from their GitHub release pages (links in `README.md`):
RED4ext, Cyber Engine Tweaks, redscript, ArchiveXL, TweakXL, Codeware, Input Loader.
Install each per its own instructions and start the game once to confirm it runs.

> **Warning: game version.** This build is upstream CyberpunkMP, which targets game
> patch **2.2**. On a newer patch it will most likely not load. Check the main-menu version
> before testing; see `docs/ARCHITECTURE_NOTES.md` §9.

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
3. Optional (probes): copy `Mods\ChoomCoop\CET\ChoomCoopProbes\` into
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
2. **Host joins.** Start game A with `--online --ip=127.0.0.1`. Load the host test save.
   - [ ] `red4ext/logs/red4ext-*.log` shows CyberpunkMP loaded (no "failed to load").
   - [ ] No redscript compile-error popup. If one appears, copy its text and
     `r6/logs/redscript_rCURRENT.log`, then stop.
   - [ ] Server console: `[ChoomCoop:Session] Player joined id=<n> name=<name>`
   - [ ] CET console (open overlay): `[ChoomCoop:Core] Client scripts loaded v0.0.1 protocol v1`
3. **Ping/Pong (host).** About 3 seconds after connecting:
   - [ ] CET console: `[ChoomCoop:Net] Connected to server, Ping scheduled in 3s`
   - [ ] CET console: `[ChoomCoop:Net] Ping sent nonce=<N> protocol=1`
   - [ ] Server console: `[ChoomCoop:Net] Ping from player=<id> nonce=<N> protocol=1 -> Pong`
   - [ ] CET console: `[ChoomCoop:Net] Pong received nonce=<N> serverProtocol=1` (same N)
4. **Guest joins.** Start game B with `--online --ip=<host IP>`, load the guest test save.
   - [ ] Server: second `Player joined` line. Steps 2-3 lines appear on the guest's CET console too.
5. **See each other.** Walk both characters to the same spot (e.g. both fast travel to
   the same point, or load saves in the same district; players far apart are not synced).
   - [ ] Host sees the guest's character move; guest sees the host.
   - [ ] Appearance roughly matches (body, clothes).
6. **Leave.** Guest quits to the main menu.
   - [ ] Server: `[ChoomCoop:Session] Player left id=<n>`; host no longer sees the guest.
7. **RPC stub check (once, either PC, server not needed).** Launch the game with
   `--online --rpc --rpcdir="C:\temp\cpmp-rpc"`. It exits by itself after loading.
   - [ ] Send me `C:\temp\cpmp-rpc\Server\Protocol.CoopServer.cs` and
     `C:\temp\cpmp-rpc\Client\Protocol.CoopClient.cs`. I'll compare their ids with
     `Shared/Protocol/RpcIds.cs`. (If the folder is empty, send `CyberpunkMP.log`.)

**What to send back:** pass/fail per checkbox, the server console text, the CET console
lines starting with `[ChoomCoop`, and any error popups. If something fails, also
`red4ext-*.log`, `CyberpunkMP.log` and `redscript_rCURRENT.log`.

Results:
| Date | Build (commit) | Game version | Result | Notes |
|---|---|---|---|---|
| | | | | |

---

## Phase 0 research probes

These answer engine questions that can't be settled without running the game. Install the
probe CET mod (step 3 of the install), load a test save **single-player is fine (no
`--online` needed)**, open the CET overlay and press the buttons. Send back every
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
