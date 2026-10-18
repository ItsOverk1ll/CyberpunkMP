# Choom Co-op — Seamless Story Co-op for Cyberpunk 2077

> Working name. "Choom" = friend in Night City slang. Rename freely.

A mod that adds **Dying Light 2–style seamless co-op** to Cyberpunk 2077: one player (the **Host**) plays their story; friends (**Guests**) drop in with their own characters, help with the story, run their own side gigs, teleport to each other on demand, and keep their progress when they leave.

This plan builds **on top of CyberpunkMP** (open-source framework by Tilted Phoques, the Skyrim Together team), not from scratch.

---

## 1. Goals and non-goals

### Goals
1. **Seamless drop-in / drop-out.** Join a friend from an invite or IP in under a minute. No separate "co-op save" setup.
2. **Host owns the story.** Only the Host's main-quest state advances. Guests participate.
3. **Everyone has freedom.** Guests can roam the open world and run their own side gigs, NCPD scanner hustles, and activities while the Host does story.
4. **Teleport on demand.** "Teleport to player" and "Join Host's mission" from a radial menu, with sensible restrictions.
5. **Guests keep their progress.** XP, street cred, eddies, items, and crafting components earned in a session carry back to the Guest's own single-player save.
6. **Save safety above all.** Joining a session must never corrupt or alter a Guest's story state.
7. **Easy to use.** One-click host, one-click join, clear UI. Minimal config.

### Non-goals (for v1)
- True multi-V story (dialogue choices by multiple players, shared romance, etc.).
- Large public servers / RP / PvP. CyberMP and CyberpunkMP already target that.
- Console support.
- More than 4 players per session.
- Sync of ambient crowds and traffic (cosmetic, local-only is fine).

---

## 2. Foundation: what we build on

| Piece | Role |
|---|---|
| **CyberpunkMP** (fork) | Player/appearance/equipment/movement/animation sync, vehicle + passenger sync, .NET server plugin SDK, Redscript client SDK, automatic RPC between client and server plugins. Built with xmake. |
| **RED4ext** + **RED4ext.SDK** | Native C++ plugin loading and engine hooks. |
| **Redscript** | Game-script-level mod code (quest/journal/mappin/UI hooks). |
| **Cyber Engine Tweaks (CET)** | Lua console + ImGui for fast prototyping and debug overlays. |
| **ArchiveXL / TweakXL** | Adding custom records (UI, mappins, items) without overwriting base files. |
| **WolvenKit** | Inspecting `.quest`, `.scene`, `.journal` files to understand quest graphs and facts. |
| **Codeware** | Extra scripting APIs (useful for UI, entity spawning, reflection). |

**Why fork instead of starting fresh:** netcode, player puppets, and vehicle sync are many months of work that already exist. Our value is the co-op session layer.

**Before writing any code:** read the CyberpunkMP `LICENSE` and confirm our fork and redistribution are allowed. Credit Tilted Phoques prominently. Never redistribute CDPR game files. Players must own the game.

---

## 3. Core design: the Host/Guest session model

### 3.1 Roles
- **Host**: runs their normal save. Their world is the source of truth for story, quest facts, time of day, weather, and mission NPCs.
- **Guest**: brings their own character (appearance, level, attributes, perks, cyberware, inventory). Their *own story state* is frozen while in session.
- **Server**: by default the Host runs a local CyberpunkMP server (listen-server style). A dedicated server is possible later but not needed for friend co-op.

### 3.2 Guest Mode (save safety, the most important system)
When a Guest joins:
1. **Snapshot**: copy the Guest's current save folder to `ChoomCoop/backups/<timestamp>/` before anything else.
2. **Freeze**: disable autosave, quicksave, manual save, and story-quest progression on the Guest client. The Guest's journal is put in a read-only "visiting" state.
3. **Ledger**: every reward the Guest earns in session (XP by type, street cred, eddies, items by TweakDB ID, components, perk points) is appended to a **Progress Ledger** (JSON on disk, written after every change so a crash loses nothing).
4. **Leave**: on disconnect (clean or crash), the Guest reloads their own save from the snapshot point and the ledger is applied through an explicit **carry-back whitelist**.
5. **Never carried back**: quest facts, journal states, world flags, NPC deaths, relationship values, cyberpsycho/gig completion flags belonging to the Host's world, vehicle ownership unlocks from Host quests (configurable later).

> Rule: if a value isn't on the whitelist, it does not leave the session.

### 3.3 Story participation (DL2 model)
- The Host starts a main job. Every Guest gets a prompt: **Join mission** (teleport to Host) or **Stay** (keep doing their thing).
- **Combat and traversal sections:** Guests fully participate. Mission NPCs are Host-simulated and replicated (see §4.3).
- **Scripted scenes** (dialogue, car rides, braindances, sitting scenes): only the Host plays the scene. Guests are placed in **Spectator Mode** near the Host (or see a "Host is in a scene" card with a timer) and can't break the scene.
- **Quest credit (optional, later):** if a Guest's own save is at the *same* quest stage, offer "Mark this quest complete in my save too" on leave. This is the only exception to the whitelist and must be opt-in per quest and very carefully validated.

### 3.4 Side content ownership
- Any player can start a gig, NCPD scanner activity, cyberpsycho hunt, or race. The starter **owns** that activity.
- Activity owner's client simulates its NPCs; other players who come near see replicated NPCs and can help.
- Rewards: owner gets normal rewards. Helpers get a share recorded in their ledger.
- All players see each other's active activities on the map with a distinct co-op mappin.

### 3.5 Teleport rules
| Action | Allowed when | Notes |
|---|---|---|
| Teleport to player | You are out of combat, not in a scene, not in a vehicle as driver | Target must not be in a restricted zone (e.g. scene volume, interior that's not streamed) |
| Join Host's mission | Host started a main job and the prompt is open | Places you at a safe spawn near the Host |
| Return to where I was | Within 5 minutes of a teleport | Lets Guests go back to their own gig |
| Summon (Host only) | Rarely, only to start a mission | Guests can decline |

Teleport flow: request → server validates → target client sends a **safe position** (navmesh-snapped, not inside geometry) → requester fades screen ("braindance sync" effect) → teleport → wait for streaming to finish → fade in. Add a timeout and fallback to nearest fast-travel point.

### 3.6 World authority
| State | Authority |
|---|---|
| Time of day, weather | Host |
| Main quest facts / journal | Host |
| Mission NPCs (story) | Host |
| Activity NPCs (gigs) | Activity owner |
| Ambient crowds / traffic | Local only, not synced |
| Wanted level / NCPD heat | Per player in v1. Shared later if it feels better. |
| Player stats, inventory, cyberware | Each player for themselves |
| Loot containers in missions | Instanced per player (each player sees their own loot). Avoids loot fights and sync complexity. |

### 3.7 Group spread
Every client runs its own game world; replicated NPCs only exist inside **sync bubbles** around activities. If players are far apart, each just runs their own local world. Limit: max 2 active NPC sync bubbles per session in v1 (main mission + one activity) to keep bandwidth and CPU reasonable. Show a warning if a third activity is started.

---

## 4. Technical architecture

```
┌─────────────── Game client (each player) ───────────────┐
│ CyberpunkMP client (C++ / RED4ext)                       │
│   └─ ChoomCoop native module (C++)                       │
│        - save snapshot + Guest Mode freeze hooks         │
│        - teleport + streaming wait                       │
│        - NPC replication (puppet spawn/drive)            │
│ ChoomCoop Redscript layer                                │
│        - quest/journal observers (Host)                  │
│        - scene detection → Spectator Mode                │
│        - mappins, prompts, radial menu, HUD              │
│ Debug overlay (CET Lua + ImGui, dev builds only)         │
└──────────────────────────┬──────────────────────────────┘
                           │ CyberpunkMP RPC (automatic)
┌──────────────────────────▼──────────────────────────────┐
│ CyberpunkMP server (.NET)                                │
│   └─ ChoomCoop.Server plugin (C#)                        │
│        - Session manager (host/guest roles, invites)     │
│        - Teleport validation                             │
│        - Activity registry (owner, bubble, helpers)      │
│        - NPC bubble authority + relay                    │
│        - Reward distribution → Guest ledgers             │
│        - Story phase broadcasts (scene start/end, etc.)  │
└──────────────────────────────────────────────────────────┘
```

### 4.1 Repository layout (inside the fork)
```
/Code/...                     (existing CyberpunkMP code, touch as little as possible)
/Mods/ChoomCoop/
  /Server/ChoomCoop.Server/   C# server plugin (.NET)
  /Server/ChoomCoop.Server.Tests/  xUnit tests (run without the game!)
  /Native/                    C++ RED4ext module (only if Redscript can't do it)
  /Scripts/                   Redscript sources (*.reds)
  /CET/                       Lua debug overlay (dev only)
  /Archive/                   ArchiveXL/TweakXL yaml/xl files
  /Shared/Protocol/           Message/RPC definitions + version number
/docs/
  ARCHITECTURE_NOTES.md       What we learned about CyberpunkMP internals
  PROTOCOL.md                 Every RPC/message, direction, fields, version
  TESTING.md                  Manual in-game test scripts per feature
  DECISIONS.md                Short ADR log
/tools/
  package.ps1                 Builds a release zip in Vortex/MO2-compatible layout
```

### 4.2 Networking principles
- Use CyberpunkMP's RPC system; don't add a second network stack.
- Every message carries a `ProtocolVersion`. Mismatched versions refuse to join with a clear message.
- Server is authoritative for: session membership, teleport approval, activity ownership, reward distribution.
- Sync rates: player transforms are already handled by CyberpunkMP. NPC bubble transforms at 10–20 Hz with interpolation; combat events (hit, death, ability use) as reliable events.
- Hits: the shooter's client reports a hit on a replicated NPC → server forwards to the NPC's authority (Host or activity owner) → authority applies damage and broadcasts results. Accept the latency for v1.

### 4.3 NPC replication (hardest technical system)
1. Authority client tags NPCs belonging to the active mission/activity (by quest/activity ownership or spawn area).
2. Authority sends spawn info: entity record/appearance ID, position, faction, health, weapon.
3. Non-authority clients spawn a **puppet** NPC that has its AI disabled and is driven by replicated transform/animation state.
4. Puppets forward incoming damage to the authority instead of applying it locally.
5. Death, ragdoll, and loot drop are authority-driven.
6. Start with: position + rotation + alive/dead + health. Add animation state and weapon firing later.

### 4.4 Scene detection
Find a reliable signal for "player is in a scripted scene" (scene system workspot, `Locomotion` restrictions, `SceneTier` / game restrictions on the player). When the Host enters scene tier 3+ (restricted control), broadcast `HostSceneStarted`; Guests near the Host enter Spectator Mode. Identify the exact API during Phase 0 research.

### 4.5 Rewards and the ledger
- Hook XP, street cred, money, item add events on the Guest client while in Guest Mode.
- Write each change to `ChoomCoop/ledger/<sessionId>.json` immediately (append-only, with checksums).
- On return to own save, apply ledger entries through the whitelist, then archive the ledger. Show a summary screen: "You earned X XP, Y eddies, Z items".
- Ledger application must be **idempotent** (re-applying the same ledger twice changes nothing).

---

## 5. User experience

- **Host:** Main menu or in-game CET/ink menu → "Host Co-op Session" → choose Friends-only / IP / invite code → share code.
- **Guest:** "Join Co-op Session" → enter code/IP → pick which of their saves to bring a character from → load in near the Host.
- **Radial menu** (hold a configurable key): Teleport to [player], Join mission, Return, Ping location, Session info.
- **HUD:** party list with names, distance, health, and current activity (e.g. "Riley: Gig – Old Friends").
- **Map:** co-op mappins for each player and each active activity.
- **Notifications:** "Wyatt started a main job: The Pickup. Join?" with a timer.
- **Leave:** "Leave session" → summary of earned rewards → back to own save.

---

## 6. Phased roadmap

Each phase ends with a working, testable build. Don't start the next phase until the current one passes its test script in `docs/TESTING.md`.

### Phase 0 — Foundation & research (≈2–4 weeks hobby time)
- Fork CyberpunkMP, get it **building and running** with two game instances or two PCs.
- Note the current build issue in their README (protobuf-cpp); fix or work around and document.
- Confirm the supported game version and RED4ext/CET/redscript versions; pin them in `docs/ARCHITECTURE_NOTES.md`.
- Map CyberpunkMP internals: server plugin lifecycle, RPC definitions, client Redscript SDK, how players are spawned/synced.
- Research APIs (document with file/line references and short code samples):
  - Teleport + wait-for-streaming
  - Saving: how to block saves, find save paths, detect save load
  - Quest/journal: observing quest state changes and facts on Host
  - Scene detection signal
  - XP / money / item-added events
  - Mappin creation for custom markers
  - Spawning an NPC and disabling its AI (puppet)
- Create empty `ChoomCoop.Server` plugin that loads and logs "hello" on server start; empty Redscript module that logs on game start.
- **Exit criteria:** two players in Night City via the fork, both seeing each other; ChoomCoop plugin and scripts both load and log.

### Phase 1 — Sessions + teleport (≈3–5 weeks)
- Session manager: Host/Guest roles, invite code, protocol version check, max 4 players.
- Party HUD (names, distance).
- Teleport to player with validation, fade, streaming wait, fallback, and "Return".
- Co-op mappins for players on the map.
- Server unit tests for session + teleport validation rules.
- **Exit criteria:** host + 1 guest, guest teleports to host and back 20 times without falling through the world or crashing.

### Phase 2 — Guest Mode & save safety (≈3–4 weeks)
- Save snapshot on join; block saving in Guest Mode; restore on leave and after crash (detect stale snapshot on next launch and offer restore).
- Progress Ledger with whitelist and idempotent apply.
- Reward summary screen.
- **Exit criteria:** guest joins, earns XP/money/items by killing street enemies, leaves; their save has exactly the whitelisted gains and no story changes. Force-kill the game mid-session; next launch recovers cleanly.

> Phase 2 comes before gameplay sync on purpose. A co-op mod that eats saves dies on the spot.

### Phase 3 — Shared activities (≈6–10 weeks)
- Activity registry: detect when a player starts a gig/NCPD activity/cyberpsycho; broadcast with mappin.
- NPC sync bubble v1: position, rotation, health, alive/dead for activity NPCs.
- Hit forwarding to authority; deaths replicated.
- Helper reward sharing via ledger.
- **Exit criteria:** player A starts an NCPD assault-in-progress; player B teleports in; both kill enemies and see the same enemies die; both get rewards.

### Phase 4 — Story co-op (≈8–12+ weeks)
- Host main-job start detection → "Join mission" prompt.
- Mission NPC bubble (reuse Phase 3).
- Scene detection → Spectator Mode for Guests; "Host is in a scene" card.
- Guest teleport-in safeguards during missions (no joining mid-scene).
- Start with **3 hand-tested main jobs** (e.g. early Act 1 jobs), document per-quest quirks, then expand.
- **Exit criteria:** host + guest complete 3 main jobs together; guest's own save unaffected.

### Phase 5 — Polish & release (ongoing)
- Join-in-progress, reconnect after disconnect, desync recovery ("resync bubble" button).
- Settings menu (keybinds, notifications, spectator preferences).
- Packaging for Vortex/MO2, install guide, compatibility list of common mods.
- Optional: Steam friends invites, quest-credit opt-in, shared wanted level, shared vehicle trips (already supported by CyberpunkMP vehicle passengers).

---

## 7. Risks & mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Game patch breaks hooks | High | Pin game version; keep native hooks minimal; prefer Redscript; isolate offsets in one file. |
| Quest scripts assume one V | High | Never sync quests to Guests; Spectator Mode for scenes; per-quest test list. |
| Save corruption | Critical | Phase 2 before gameplay; snapshot + freeze + whitelist ledger; crash recovery. |
| NPC desync | High | Authority model; small bubbles; resync button; start with minimal state. |
| Upstream CyberpunkMP changes | Medium | Keep our code in `/Mods/ChoomCoop`; touch `/Code` only when necessary; rebase regularly. |
| License/legal | Medium | Check LICENSE first; credit upstream; don't redistribute game files. |
| Scope creep | High | Phase exit criteria; non-goals list; DECISIONS.md. |
| Solo-dev burnout | High | Small shippable phases; Phase 1 alone is already fun to show friends. |

---

## 8. Development workflow with Claude Code

- Claude Code **cannot launch or see the game**. It writes code, builds, runs server unit tests, and adds logging. Wyatt runs in-game tests and pastes logs back.
- Every feature ships with: server unit tests where possible, a manual test script in `docs/TESTING.md`, and verbose logs behind a debug flag.
- Logs: one log file per subsystem in `<game>/red4ext/logs/` or the plugin's own folder, prefixed `[ChoomCoop:<Subsystem>]`.
- Small commits, one feature per branch, PR-style summaries in commit messages.
- When unsure about an engine API, Claude Code should **research and propose** (with references) before implementing, and record findings in `docs/ARCHITECTURE_NOTES.md`.

---

## 9. Testing setup (Wyatt's side)

- Two machines with a legit copy each, **or** one machine running two instances if CyberpunkMP supports it (verify in Phase 0).
- A dedicated **test save** at an early Act 1 point for Host, and a separate test save for Guest. Never test with your real playthrough saves.
- Pinned mod stack: CyberpunkMP fork build, RED4ext, CET, redscript, ArchiveXL, TweakXL, Codeware. No other mods during testing.
- Keep a `TESTING.md` checklist and record pass/fail per build.

---

## 10. Definition of v1.0
- 2–4 players, invite code join, stable for a 2-hour session.
- Teleport to player / join mission / return.
- Guest Mode with zero story contamination and crash recovery.
- Shared NCPD activities and gigs.
- At least 10 main jobs verified co-op-playable, with a public list.
- Packaged install with clear docs.
