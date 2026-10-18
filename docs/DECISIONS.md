# Decisions

Short log: date, decision, why, alternatives.

## 2026-10-18 — Tester installer: ChoomCoopSetup.exe
- **Decision:** a self-contained single-file .NET console app (`Mods/ChoomCoop/Installer`)
  that embeds our build and downloads the six frameworks from their official releases with
  pinned SHA-256s. No admin rights. The exe is a build output and is not committed.
- **Why:** testers need the exact framework versions and correct folder layout; downloading
  instead of bundling keeps third-party binaries out of our artifact; hashes stop a changed
  upstream asset from installing silently.
- **License:** our build is a modified CyberpunkMP, so the exe must be distributed only via
  our public GitHub (e.g. a Release on the fork) or our own site, never a mod site.
- **Alternatives:** PowerShell script (needs execution-policy changes on the friend's PC);
  bundling the frameworks (bigger, redistributes others' binaries); the fork's launcher.

## 2026-10-18 — Target game patch 2.31; port fixes from Night City Online selectively
- **Decision:** stay on upstream CyberpunkMP and port only the game-compatibility and crash
  fixes from `ofmiceandcam98-eng/CyberpunkMP` (branch `phase0/patch-2.31`):
  - `9732c46` (as-is minus build files): RTTI name resolution (the game would not start on
    2.31), worldTransform positions, spawn/appearance null guards, PuppetRegistry.
  - `c346f5a` (code only): appearance use-after-free and ccstate under-run.
  - `58881c8` (core only, rewritten): dangling `c_str()` in `Main.cpp`.
  - Vehicle threading race diagnosed in `0fa2bb9`: fixed our own way (below).
- **Not taken:** protobuf 29.3 pin (our pinned repo gives a working 28.1); `fbb7b33`
  (needs fork-only code, and its trigger is the fork's inventory autosave); `5159dc4`
  (RPC table sizing: defensive, ids are dense today; revisit if we see unknown-id logs);
  `d014c94` and its prerequisites (no D3D12 objects when the overlay is off — needed for CET
  compatibility, deeper change); everything else (launcher, Discord, phone, persistence).
- **Why:** Wyatt is on Steam 2.31. Starting from scratch would cost months for netcode we
  already have; rebasing onto the fork would pull in ~1,000 commits of RP-server features.
  Each port keeps the original author and is credited in the commit message.
- **Alternatives:** full rebase onto the fork (revisit if we keep needing its fixes);
  downgrade the game to 2.2.

## 2026-10-18 — Upstream edit: vehicle callbacks and ThreadService run on the main thread
- **Decision:** `VehicleSystem::OnVehicleEnter/Exit/Ready` defer their bodies with
  `ThreadService::RunInMainThread`; `ThreadService::OnGameUpdate` pops and runs tasks outside
  the queue lock.
- **Why:** redscript-called natives can arrive on job threads and these touch the
  single-threaded flecs world (the fork caught it crashing the client). The fork's fix
  needs a 169-line registry from later commits; upstream already has the main-thread queue.
  Running tasks outside the lock lets a task queue another without deadlocking.
- **Risk:** enter/exit now take effect one frame later. Needs an in-game vehicle test.

## 2026-10-18 — Multiplayer tests run without CET; the server log is the witness
- **Decision:** add `CoopServer.PongAck` (client confirms it received Pong) so the server
  console proves the whole round trip. Protocol stays v1 (nothing has shipped yet).
- **Why:** CET's overlay plus CyberpunkMP's renderer hard-locked GPUs in the fork, and our
  client logging otherwise only shows in the CET console.

## 2026-10-18 — Build toolchain pins (no upstream code changes)
- **Decision:** build with xmake 2.9.9, a local xmake-repo clone pinned to `704a10506`
  (2024-12-21) registered with `xmake repo --add --global`, Windows SDK 10.0.18362.0,
  pnpm 9 from a private tools folder, current 7-Zip.
- **Why:** today's package repo resolves incompatible versions (abseil vs
  gamenetworkingsockets); xmake 2.9.6 picks a non-existent `v144` toolset for MSVC 14.44;
  upstream README requires SDK < 26100; pnpm 10+ blocks build scripts.
- **Alternatives:** enable `package.requires_lock` (lock file is stale and needs an
  upstream edit); downgrade MSVC (heavy).

## 2026-10-18 — Upstream edit: SdkGenerator uses CppSharp 1.1.84 on Windows
- **Decision:** `code/server/scripting/SdkGenerator/SdkGenerator.csproj`: net8.0 -> net9.0,
  CppSharp 1.1.5.3168 -> 1.1.84.17100 when `$(OS) == Windows_NT` (Linux keeps the old pin).
- **Why:** CppSharp 1.1.5 bundles Clang < 19; MSVC 14.44's STL refuses it (`STL1000`), so
  the C# SDK bindings were never generated and the whole server build failed. Same fix as
  the Night City Online fork. Unavoidable without downgrading Visual Studio.
- **Alternatives:** define `_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH` in the generator
  (also an upstream edit, less tested); install MSVC 14.38.

## 2026-10-18 — Upstream edit: root xmake.lua includes Mods/ChoomCoop
- **Decision:** one line, `includes("Mods/ChoomCoop")`.
- **Why:** so `xmake` builds and installs our plugin and scripts with everything else.
  Upstream's plugin scan only looks in `code/server/scripting/*System`.
- **Alternatives:** separate build script (easy to forget); moving our plugin into
  `code/server/scripting` (mixes our code with upstream's).

## 2026-10-18 — Plugin assembly name `ChoomCoopSystem`
- **Decision:** project folder `ChoomCoop.Server` (PLAN.md layout), assembly and root
  namespace `ChoomCoopSystem`.
- **Why:** the loader only loads `plugins/<Name>/<Name>.dll` with type `<Name>.Plugin`
  and strips a `System` suffix.

## 2026-10-18 — Hand-written RPC stubs, verified by tests
- **Decision:** write the C# RPC stubs by hand in the generator's exact format; keep ids in
  `Shared/Protocol/RpcIds.cs`; unit tests recompute them (FNV-1a 64).
- **Why:** the official generator needs a game launch with `--rpc` per change and dumps
  every plugin's types into one folder. The hash rule was verified against upstream's
  generated EmoteSystem ids.
- **Alternatives:** generate with `--rpc` each time (still used once in Phase 0 to confirm).

## 2026-10-18 — Testable server logic lives in `Core/` without SDK references
- **Decision:** the test project compiles `ChoomCoop.Server/Core/**` and
  `Shared/Protocol/*.cs` directly instead of referencing the plugin.
- **Why:** CyberpunkSdk needs CppSharp-generated code and the native server, so tests that
  reference it cannot run on their own. One shipped DLL also matters: the install step
  deletes extra DLLs from plugin folders.

## 2026-10-18 — Client scripts install to `r6/scripts/ChoomCoop/` for now
- **Decision:** dev installs copy our `.reds` to `<game>/r6/scripts/ChoomCoop/`.
- **Why:** simplest and standard. Later we can ship via CyberpunkMP's `--mod=<folder>`
  flag so the scripts only load with the multiplayer mod.

## 2026-10-18 — License: ship Choom Co-op as a plugin, never on third-party mod sites
- **Decision:** keep ChoomCoop a "Plugin" in LICENSE.md §2.1 terms where possible
  (uses the SDK, does not modify or redistribute CyberpunkMP). Our two small upstream
  edits mean our *fork* is a modification: it must stay public under the same license,
  credit Tilted Phoques, and be distributed only on GitHub / our own site — **not Nexus**.
- **Why:** LICENSE.md §1.3 and §2.1. Open question for Wyatt: whether co-op "competes
  directly" with CyberpunkMP (§1.2). Plugin status sidesteps that clause.
