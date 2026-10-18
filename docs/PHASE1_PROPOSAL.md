# Phase 1 proposal — Sessions + teleport

Status: **proposal, not implemented.** For Wyatt's review.
Scope from `PLAN.md` §6 Phase 1: session manager (Host/Guest roles, invite code, protocol
version check, max 4 players), party HUD (names, distance), teleport to player with
validation, fade, streaming wait, fallback and Return, co-op mappins, server unit tests.
Exit criteria: host + 1 guest, guest teleports to host and back 20 times without falling
through the world or crashing.

**Precondition:** Phase 0 exit criteria pass, and the game-version question
(`ARCHITECTURE_NOTES.md` §9) is settled. If we rebase onto the 2.31 fork, do that first:
Phase 1 code is all ChoomCoop-side and should carry over unchanged.

---

## Design summary

- **One session per server** in v1 (the server is the Host's listen server). The first
  player to call `CreateSession` becomes Host; the server issues a 6-character invite code.
  Joining needs the code. Simple and matches "friend co-op".
- **Server is authoritative** for membership, roles and teleport approval. Clients send
  requests; the server validates with pure, unit-tested rules in `Core/`.
- **The teleport target computes the destination**, because only its client has the
  navmesh streamed there (PLAN.md §3.5). The server just relays and validates.
- **Client state that matters for validation** (in combat, in scene, driving) is reported
  by each client on change (`ReportPlayerState`) so the server can validate without asking.
- Player positions for the HUD and pins come from the server (`MovementComponent`), sent
  at 2 Hz in one batched `PartyUpdate`. No per-frame RPCs.

---

## RPCs (protocol v2)

Client -> server (`CoopServer`), all carry `protocolVersion` first:

| RPC | Args | Notes |
|---|---|---|
| `CreateSession` | version | Caller becomes Host. Fails if a session exists. |
| `JoinSession` | version, code: String | Fails: bad code, full (4), version mismatch. |
| `LeaveSession` | version | Host leaving ends the session for all (v1). |
| `ReportPlayerState` | version, inCombat: Bool, sceneTier: Int32, isDriver: Bool | On change only. |
| `RequestTeleport` | version, targetPlayerId: Uint64 | Starts the flow below. |
| `TeleportDestination` | version, requestId: Uint32, ok: Bool, pos: Vector4, yaw: Float | Sent by the target client. |
| `TeleportFinished` | version, requestId: Uint32, success: Bool, reason: String | Requester reports outcome (for logs/stats). |
| `RequestReturn` | version | Return to the pre-teleport position (within 5 min). |

Server -> client (`CoopClient`):

| RPC | Args |
|---|---|
| `SessionState` | sessionId, role (0 none/1 host/2 guest), inviteCode (host only), members: array<CoopMember> |
| `SessionError` | code: Int32, message: String |
| `PartyUpdate` | members: array<CoopMemberPos> (id, x, y, z) |
| `FindTeleportDestination` | requestId, requesterName |
| `TeleportApproved` | requestId, pos, yaw |
| `TeleportDenied` | requestId, reason: String |

Structs `CoopMember { id: Uint64, name: String, role: Int32 }` and
`CoopMemberPos { id: Uint64, x: Float, y: Float, z: Float }` (no handles, so RPC-legal).
Struct-typed args mean the C# side needs `RedTypes`-style classes; we'll hand-write them
like the stubs and add ids/layout tests.

**Teleport flow:** `RequestTeleport` -> server validates requester -> `FindTeleportDestination`
to target -> target computes a navmesh point within 3 m (`FindPointInSphereOnlyHumanNavmesh`),
not in a scene/interior restriction -> `TeleportDestination` -> server re-validates (target
still valid, request not expired after 10 s) -> `TeleportApproved` to requester -> client:
save return point, fade out, teleport, poll streaming (navmesh streamed + on ground) up to
10 s, fade in -> `TeleportFinished`. On timeout: teleport to the nearest fast-travel point.

---

## Tasks (one branch each, in order)

### 1. `phase1/session-core` — server session logic (no game needed)
- Create `Core/Sessions/SessionManager.cs`, `Session.cs`, `SessionMember.cs`,
  `InviteCode.cs` (6 chars, no ambiguous letters, injectable RNG), `SessionErrors.cs`.
- Rules: max 4; one Host; join needs code; version check; host leave ends session;
  player disconnect = leave; rejoin after disconnect allowed.
- Tests `SessionManagerTests.cs`: create/join/leave, full session, wrong code, version
  mismatch, double join, host leaves, guest disconnects, invite code format/uniqueness.

### 2. `phase1/session-rpc` — wire sessions through RPC
- `Protocol.reds`: add the session RPCs + structs; bump `ProtocolVersion` to 2 (both sides).
- `RpcIds.cs` + stubs + `RedTypes` classes; extend `RpcIdTests`.
- `Plugin.cs`: route `PlayerLeftEvent` into `SessionManager`.
- Client `Scripts/Session/SessionClient.reds`: stores state, logs every change.
- Dev UI for now: CET buttons (`Mods/ChoomCoop/CET/ChoomCoopDev/`) "Host", "Join <code>",
  "Leave", and a text field. Real UI in task 5.
- Update `docs/PROTOCOL.md`.

### 3. `phase1/teleport-rules` — teleport validation (no game needed)
- `Core/Teleport/TeleportValidator.cs`: requester & target in same session; requester not
  in combat, sceneTier < 3, not driving; target sceneTier < 3; cooldown 5 s; one pending
  request per requester; request expiry 10 s; Return only within 5 min of a successful
  teleport. `Core/Teleport/TeleportRequests.cs` tracks pending/expired (injectable clock).
- Tests `TeleportValidatorTests.cs`: each rule allow/deny, expiry, cooldown, return window,
  target leaves mid-request.

### 4. `phase1/teleport-client` — the in-game teleport
- `Scripts/Teleport/TeleportClient.reds`: handlers for FindTeleportDestination /
  Approved / Denied; navmesh point search; fade (`inkWidget` full-screen black or the
  game's fade via `GameInstance.GetUISystem` — decided by probe results); teleport;
  streaming poll via `DelayCallback` every 0.25 s; timeout fallback; return point storage.
- `Scripts/Core/PlayerStateReporter.reds`: wraps `PlayerPuppet.OnSceneTierChange`, combat
  and vehicle PSM listeners -> `ReportPlayerState`.
- Logs `[ChoomCoop:Teleport]` for every step with timings (that's how we'll debug falls).
- Depends on probes P1, P2, P4.

### 5. `phase1/party-hud` — party HUD + pins
- Server: `PartyUpdate` at 2 Hz from `MovementComponent` positions (`World.UpdateEvent`
  with an accumulator; pure `PartyUpdateScheduler` tested).
- Client: `Scripts/UI/PartyHud.reds` — Codeware `inkCustomController` list (name +
  distance in metres), top-left; `Scripts/UI/CoopMappins.reds` — `RegisterMappinWithObject`
  on each remote muppet (moving pin), removed on leave.
- Teleport entry point: a simple selection list (EmoteSelector-style) bound to a key
  through Input Loader; the radial menu is Phase 1.5 if the list works.

### 6. `phase1/test-pass` — in-game test & polish
- Write the Phase 1 script in `docs/TESTING.md` (below), Wyatt runs it, fix findings.

---

## Unit tests to write (all in `ChoomCoop.Server.Tests`, run with `dotnet test`)

- `SessionManagerTests` (~15 cases), `InviteCodeTests` (format, collisions with seeded RNG)
- `TeleportValidatorTests` (~20 cases incl. clock-driven expiry/cooldown/return)
- `PartyUpdateSchedulerTests` (rate, no sends without session)
- `RpcIdTests` extended for every new RPC; a test that every `[ServerRpc]`/`[ClientRpc]`
  id in the plugin assembly appears in `RpcIds` (reflection over the plugin dll is not
  possible without the SDK, so: a source-text scan of `Rpc/**/*.cs`).

---

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Streaming wait signal unreliable (P1) | Falling through world | Combine navmesh-streamed + ground raycast + min 1 s wait; fallback teleport point; log timings. |
| Teleport across cells: CyberpunkMP unloads/reloads puppets | Brief invisible players, puppet flicker | Expected; verify no crash. Possibly delay HUD pin update until `NotifyCharacterLoad`. |
| Target in an interior that isn't streamed for requester | Wrong floor / outside building | Target-side navmesh point; deny when target sceneTier >= 3; fallback point. |
| Struct RPC args by hand | Wire mismatch | Mirror generator format exactly; `--rpc` run once to compare; tests. |
| Host-is-server assumption | Host crash ends session | Accept for v1; documented. |
| Game version / base fork change | Rework | Decide before task 1; ChoomCoop code is fork-independent. |
| Redscript compile errors only seen in-game | Whole mod disabled | Small script changes per test build; ask Wyatt for `redscript_rCURRENT.log` on any popup. Consider adding a script compile check tool later (needs game files). |

---

## Phase 1 in-game test script (draft)

Setup: host + 1 guest, both on the same build, server on host.

1. Host: "Host" button -> CET shows invite code; server logs `Session created`.
2. Guest: "Join <code>" -> both HUDs list both names; wrong code shows an error.
3. Guest walks 200 m away -> HUD distance updates; map shows a co-op pin on the host.
4. Guest: teleport to host -> fade, arrives within 3 m of host on the ground, fade in.
   Log shows streaming wait time.
5. Guest: Return -> back at the original spot.
6. Repeat 4-5 **20 times** across: open street, indoors (Megabuilding hallway), rooftop,
   Badlands. Record any fall/stuck/crash with the `[ChoomCoop:Teleport]` log lines.
7. Denials: guest in combat -> denied with reason; host in a dialogue scene -> denied;
   guest driving -> denied.
8. Guest leaves -> host HUD removes guest, pin disappears; host leaves -> guest gets
   "session ended".

Pass = steps 1-8 with zero falls/crashes in the 20 teleports.
