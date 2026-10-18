# Decisions

Short log: date, decision, why, alternatives.

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
