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

## Build (verify and update this section during Phase 0)
- Upstream: `xmake -y` from repo root (add `-v` for verbose).
- VS solution: `xmake project -k vsxmake` → open the `.sln` in `vsxmake/`.
- Debug against the game: `xmake f --game="C:/.../Cyberpunk2077.exe"`.
- Upstream README noted a protobuf-cpp build issue; record the fix/workaround here.
- Server Docker image: `docker build . -t cyberpunkmp`, run with `-p 11778:11778`.
- ChoomCoop server tests: `dotnet test Mods/ChoomCoop/Server/ChoomCoop.Server.Tests` (create in Phase 0).

## Pinned versions (fill in during Phase 0)
- Cyberpunk 2077: _TBD_
- RED4ext: _TBD_
- Cyber Engine Tweaks: _TBD_
- redscript: _TBD_
- ArchiveXL / TweakXL / Codeware: _TBD_
- CyberpunkMP upstream commit: _TBD_

## Key docs
- `PLAN.md` — design, roadmap, exit criteria
- `docs/ARCHITECTURE_NOTES.md` — what we learned about CyberpunkMP and engine APIs
- `docs/PROTOCOL.md` — all RPCs/messages
- `docs/TESTING.md` — manual in-game test scripts
- `docs/DECISIONS.md` — short decision log (date, decision, why, alternatives)

## Current phase
**Phase 0 — Foundation & research.** See `PLAN.md` §6.
