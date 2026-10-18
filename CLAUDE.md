# CLAUDE.md — Choom Co-op

Project context for Claude Code. Read this first every session, then read `PLAN.md` for the full design.

## What this is
A seamless, Dying Light 2–style co-op mod for Cyberpunk 2077, built as a layer on top of a fork of **CyberpunkMP** (Tilted Phoques). The Host plays their story; Guests drop in with their own characters, help, run their own side activities, teleport to each other, and keep whitelisted progress when they leave.

## Owner / tester
- Wyatt, on Windows. He runs all in-game tests and reports logs back.
- **You cannot launch the game or see it.** Never claim something works in-game. Say "builds and unit tests pass; needs in-game test" and give him a short test script.

## Golden rules
1. **Save safety first.** Nothing may modify a Guest's story state. Anything leaving a session goes through the carry-back whitelist in `PLAN.md` §3.2. When in doubt, don't carry it back.
2. **Our code lives in `/Mods/ChoomCoop/`.** Touch upstream `/Code/` only when unavoidable, keep the diff minimal, and log why in `docs/DECISIONS.md`.
3. **Prefer Redscript over native C++ hooks.** Native hooks break on game patches. If you need native code, isolate addresses/signatures in one file.
4. **Use CyberpunkMP's RPC system.** No second network stack. Every message is documented in `docs/PROTOCOL.md` and carries `ProtocolVersion`.
5. **Research before implementing engine APIs.** For unfamiliar game APIs, find evidence (decompiled redscript sources, RED4ext.SDK headers, existing mods, CyberpunkMP code), write findings with references to `docs/ARCHITECTURE_NOTES.md`, then propose an approach.
6. **Phase discipline.** Work only on the current phase in `PLAN.md` §6. Don't start the next phase until the exit criteria pass.
7. **Tests.** Server logic (sessions, teleport validation, activity registry, ledger, whitelist) must have xUnit tests that run without the game. Client features get a manual script in `docs/TESTING.md`.
8. **Logging.** Every subsystem logs with prefix `[ChoomCoop:<Subsystem>]`, verbose behind a debug flag. Logs are how we debug in-game issues.
9. **No game files.** Never commit or redistribute CDPR assets. Respect the upstream LICENSE and keep attribution.
10. **Small steps.** One feature per branch, small commits, clear messages. Summarize what changed and what Wyatt must test.

## Tech stack
- C++20, xmake (upstream build), RED4ext + RED4ext.SDK
- .NET / C# for server plugins (CyberpunkMP server SDK), xUnit for tests
- Redscript for client game logic and UI
- CET Lua + ImGui for dev-only debug overlays
- ArchiveXL / TweakXL / Codeware where needed
- WolvenKit (Wyatt's side) for inspecting quest/scene files

## Build (verified 2026-10-18 on Wyatt's PC; details in docs/ARCHITECTURE_NOTES.md §7)
One-time machine setup (none of it changes upstream code):
- Visual Studio 2022 with C++ (MSVC 14.44 works) + Windows SDK **10.0.18362.0** (anything below 10.0.26100).
- xmake **2.9.9**: `winget install --id Xmake-io.Xmake --version 2.9.9+20250408 --scope user`
  (2.9.6 picks a bogus `v144` toolset; 3.x untested).
- Pinned package repo (today's xmake-repo resolves broken versions), once:
  - `git clone %LOCALAPPDATA%\.xmake\repositories\xmake-repo %LOCALAPPDATA%\xmake-repo-pinned-20241221`
  - `git -C %LOCALAPPDATA%\xmake-repo-pinned-20241221 checkout 704a10506`
  - `xmake repo --add --global cpmp-pinned %LOCALAPPDATA%\xmake-repo-pinned-20241221`
- 7-Zip on PATH (`winget install 7zip.7zip`; xmake's bundled 7z 19.00 fails).
- pnpm **9** first on PATH for builds (global pnpm here is 12): `npm install --prefix %LOCALAPPDATA%\cpmp-buildtools pnpm@9`.
- .NET SDK 9 (SdkGenerator needs it; plugins and tests are net8.0).

Build (PowerShell, repo root):
```
$env:Path = "$env:LOCALAPPDATA\cpmp-buildtools\node_modules\.bin;C:\Program Files\7-Zip;" + $env:Path
xmake f --arch=x64 --mode=releasedbg --vs_sdkver=10.0.18362.0 -y
xmake -y -j 4
xmake install -y -o distrib   # ends with an Electron launcher packaging error; mod/server are installed before it
```
- Outputs: `build/windows/x64/releasedbg/` (server + `plugins/ChoomCoopSystem/`), `distrib/launcher/{mod,server}`,
  `distrib/choomcoop/r6/scripts/ChoomCoop/`.
- README's protobuf-cpp issue: resolved by the SDK + repo pins above (protobuf-cpp 28.1, abseil 20240722).
- Upstream edit: `SdkGenerator.csproj` uses CppSharp 1.1.84 on Windows (see DECISIONS.md). If you see
  `namespace 'Internal' does not exist`, run SdkGenerator by hand: it hides its errors.
- Run the server from a real terminal (not redirected) with `CYBERPUNKMP_ADMIN_USERNAME` / `CYBERPUNKMP_ADMIN_PASSWORD`
  set (any values), or release builds throw at startup.
- ChoomCoop server tests (no game or native build needed): `dotnet test Mods/ChoomCoop/Server/ChoomCoop.Server.Tests`.
- VS solution: `xmake project -k vsxmake`. Debug target: `xmake f --game="C:/.../Cyberpunk2077.exe"` (it symlinks the
  DLL; for real installs copy files as in docs/TESTING.md).
- Docker server image: `docker build . -t cyberpunkmp`, run with `-p 11778:11778` (not re-verified).

## Pinned versions (what this code targets)
- Cyberpunk 2077: **2.2** (upstream). Newer patches reportedly don't load it; see ARCHITECTURE_NOTES.md §9.
- RED4ext: the 2.2-era release (1.26.x, unverified); SDK submodule `f52af6a`
- Cyber Engine Tweaks: 2.2-era (1.34.x, unverified)
- redscript: 2.2-era (0.5.2x, unverified)
- ArchiveXL 1.18.1-beta (+6) / TweakXL 1.10.6 / Codeware 1.13.0 (submodules)
- Input Loader 0.2.0 (required by the client DLL)
- CyberpunkMP upstream commit: `0ccb0cfa78222cc463ea937e0c8271060f9afd2a` (2024-12-21; upstream inactive since)

## Key docs
- `PLAN.md` — design, roadmap, exit criteria
- `docs/ARCHITECTURE_NOTES.md` — what we learned about CyberpunkMP and engine APIs
- `docs/PROTOCOL.md` — all RPCs/messages
- `docs/TESTING.md` — manual in-game test scripts
- `docs/DECISIONS.md` — short decision log (date, decision, why, alternatives)
- `docs/PHASE1_PROPOSAL.md` — Phase 1 plan awaiting Wyatt's review

## Code facts that bite
- Our code: `Mods/ChoomCoop/` (upstream lives in lowercase `code/`). Server plugin assembly must be `ChoomCoopSystem`.
- RPC ids are FNV-1a 64 hashes; stubs are hand-written, ids in `Shared/Protocol/RpcIds.cs`, checked by tests.
- `ClientRpc` methods run with a null `this`. A single bad `.reds` file disables ALL scripts.
- Server logic that needs tests goes in `ChoomCoop.Server/Core/` with no CyberpunkSdk references.

## Current phase
**Phase 0 — Foundation & research.** See `PLAN.md` §6.
