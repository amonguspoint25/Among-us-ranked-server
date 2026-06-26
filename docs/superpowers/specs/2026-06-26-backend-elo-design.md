# Backend + ELO Engine — Design Spec

**Date:** 2026-06-26
**Status:** Approved (brainstorming), pending implementation plan
**Scope:** Sub-project #1 of 3. This spec covers ONLY the backend service and ELO
engine. The Ranked Host **mod** and the **website** each get their own spec.

---

## 1. Context: the whole system (brief)

A community "ranked Among Us" ladder ("0.25 ranked"). Mobile players must be able
to play with **zero install**, which is the constraint that shapes everything.

Key facts established during design (all web-verified against primary sources —
protocol docs, AutoMuteUs source, SkeldJS docs, rankedamongus.com, Innersloth CoC):

- Among Us is **host-authoritative**: the host client assigns roles/tasks and
  resolves kills/votes for **all** players, including unmodded mobile/console clients.
- The full secret state (incl. roles) is replicated to **every** client's memory,
  so any single modded PC in the lobby can read everyone's roles/kills/votes/tasks
  (this is why "role reveal" cheats exist; AutoMuteUs's own code reads the role
  offsets). The host is still the preferred capture point because it controls
  lobby settings and start.
- Mobile (esp. iOS) **cannot** connect to a custom/private server (jailbreak-only),
  so ranked games run on **official Innersloth servers** via a normal 6-char lobby
  code. A self-hosted Among Us server would *exclude* mobile.
- A protocol-level host bot (SkeldJS) on official servers is technically possible
  but a maintenance/ban trap (EOS auth, breaks nearly every patch) — not the path.

**System data flow:**

```
[Modded PC "Ranked Host"] hosts lobby on OFFICIAL servers (6-char code)
   • mobile/console/vanilla-PC players join, install NOTHING
   • mod reads roles, kills, votes→correct/incorrect, tasks, winner (all players)
   • streams live events ─────────►  [THIS BACKEND]
   • POSTs authoritative MatchReport at game-end ─►   • REST ingest
                                                      • SignalR live push
                                                      • dual-Elo compute + store
   [Website / Leaderboard]  ◄──── SignalR live updates ────┘
```

**Hosting evolution (out of scope here, noted for context):** Stage 0 operator-run
modded clients → Stage 1 mod auto-hosts on operator-controlled cloud Windows boxes
(no volunteers) → Stage 2 (only if needed) protocol host. The backend is identical
across all stages.

---

## 2. Goals & non-goals (this spec)

**Goals**

- Receive authoritative match reports from a Ranked Host mod and store them.
- Compute a **dual Elo** (Crew-Elo + Impostor-Elo → combined) per player.
- Track lifetime per-player stats: kills, correct votes, incorrect votes, tasks
  completed, wins, losses, games played.
- Push **live** match events + leaderboard changes to the website in real time.
- Expose read APIs: leaderboard, player profile + match history, match detail.

**Non-goals (deferred — YAGNI)**

- Real anti-cheat / multi-host cross-validation (leave a validator seam only).
- The mod itself and the website (separate specs).
- Accounts/passwords/login (players are identified by Among Us friend code; no
  user auth surface beyond the host ingest token).
- Matchmaking, seasons, rating decay (revisit once there is real match data).

---

## 3. Core integrity principle

**Live events are display-only; the end-of-game `MatchReport` is the single source
of truth for ELO.**

During a game the mod streams events (kill, meeting, ejection, task) purely to
animate the live UI — these are ephemeral and untrusted. Ratings are **only** ever
mutated by the authoritative end-of-game report. Consequence: a dropped, duplicated,
or spoofed live packet can never corrupt the ladder, and the rating code path stays
trivial to reason about and unit-test.

---

## 4. Components

Small, isolated, individually testable modules (many small files > few large).

### 4.1 `Elo` engine — pure, no I/O
A pure function. **Input:** match teams, winner, each player's current ratings +
game counts, the map/settings base-rate. **Output:** new ratings + per-player deltas.
No DB, no clock, no network → exhaustively unit-testable; heaviest TDD coverage.
This is the heart of the system.

### 4.2 Ingest API
- `POST /api/matches` — authoritative `MatchReport`. **Idempotent on `matchId`**.
  Host-token auth. Schema-validated at the boundary. Triggers the ELO update.
- `POST /api/matches/{matchId}/events` *(optional, live)* — lightweight event for
  the live UI. Never mutates ratings. May instead be a SignalR message from the mod;
  decided at plan time (REST is the simpler default).

### 4.3 Domain + persistence (EF Core + PostgreSQL)
Entities in §5.

### 4.4 Live hub (SignalR)
Broadcasts live match events and leaderboard/rating changes to connected web clients.

### 4.5 Read API
- `GET /api/leaderboard` — players sorted by combined Elo (paginated).
- `GET /api/players/{id}` — ratings + lifetime stats + recent match history.
- `GET /api/matches/{id}` — match detail (teams, outcome, per-player lines).

---

## 5. Data model

`Player`
- `id` (PK)
- `friendCode` (stable Among Us identity; unique; nullable until linked — see §7)
- `displayName` (latest seen)
- `crewElo` (double, default 1000), `impostorElo` (double, default 1000)
- `combinedElo` (derived = (crewElo + impostorElo) / 2; stored for cheap sorting)
- `crewGames`, `impostorGames` (int)
- lifetime stats: `kills`, `correctVotes`, `incorrectVotes`, `tasksCompleted`,
  `wins`, `losses` (all int)
- `createdAt`, `updatedAt`

`Match`
- `id` (PK; equals the mod-supplied `matchId` for idempotency)
- `startedAt`, `endedAt`
- `map` (enum/string), `impostorCount` (int), `settingsHash` (string — groups
  matches by ruleset for per-settings base-rate)
- `winningTeam` (enum: Crew | Impostor)
- `gameVersion` (string)

`MatchPlayer` (one row per player per match)
- `matchId` (FK), `playerId` (FK)
- `team` (enum: Crew | Impostor)
- `survived` (bool)
- per-match stats: `kills`, `correctVotes`, `incorrectVotes`, `tasksCompleted`
- `eloBefore`, `eloAfter`, `eloDelta` (for the rated track — crew or impostor)

---

## 6. ELO algorithm

Dual rating; **outcome-driven** (team win/loss only). Individual stats are recorded
for display, NOT fed into the rating (farmable / role-correlated — proven AU ladders
and the canonical asymmetric-rating method both rate on outcomes only). If a future
decision wants stats to nudge ELO, it slots in as a separate, capped term.

```
A_imp  = mean(impostors' Impostor-Elo)
A_crew = mean(crew's Crew-Elo)
p0     = empirical impostor win-rate for this map+settings over a rolling window
         (~100 matches; default 0.30 until enough data)
bias   = 400 * log10(p0 / (1 - p0))
E_imp  = 1 / (1 + 10^(-((A_imp - A_crew) + bias) / 400))
E_crew = 1 - E_imp
S_imp  = 1 if impostors won else 0;   S_crew = 1 - S_imp

for each impostor: Impostor-Elo += K * (S_imp  - E_imp)
for each crewmate: Crew-Elo     += K * (S_crew - E_crew)

K = 48 while the player is PROVISIONAL (crewGames < 25 OR impostorGames < 5),
    else 24
combined = (crewElo + impostorElo) / 2
all ratings start at 1000
```

Rationale for constants (from research; tunable):
- Separate crew/impostor ratings because the roles are asymmetric and a player can
  be strong at one and weak at the other.
- `bias` shifts the expectation toward the *real* impostor win-rate so two
  equally-rated teams don't get predicted at a false 50/50.
- Impostor games are ~1/4 as frequent, so impostor rating stabilizes at fewer games
  (5 vs 25) and rides a higher provisional K longer.

---

## 7. Player identity (the one open question)

ELO needs to recognize the same person across games.

- **Primary:** Among Us **friend code** (a stable per-account id). The host mod
  reads it for each player and includes it in the `MatchReport`.
- **Fallback:** if friend code is not reliably readable for all players, match by
  `displayName` with an admin "link/merge" step.

This must be confirmed when we build the mod (can the host read every player's
friend code?). The backend is designed friend-code-primary with the name fallback;
`Player.friendCode` is nullable to allow provisional name-only records that get
merged later.

---

## 8. Auth & integrity

- **Ingest auth:** the mod presents a host token (shared secret, per-operator) on
  `POST /api/matches`. Unauthenticated ingest is rejected. No player-facing auth.
- **Idempotency:** `matchId` is the `Match` PK; a re-POSTed report is a no-op.
- **Validation:** the `MatchReport` is schema-validated at the boundary; malformed
  reports are rejected with a clear error (never silently swallowed).
- **Atomicity:** the ELO update (ratings + `MatchPlayer` rows + lifetime stat
  increments) runs in **one DB transaction**.
- **Anti-cheat seam (deferred):** ingest goes through an `IMatchReportValidator`
  interface with a no-op default. Real cross-checking (e.g. requiring agreement
  across multiple hosts) plugs in here later — not built now.

---

## 9. Testing strategy

Per project rules (TDD, 80%+ coverage):

- **`Elo` engine:** pure unit tests first (RED→GREEN). Scenarios: equal teams,
  upset win, provisional K, base-rate skew (p0 ≠ 0.5), crew vs impostor tracks
  updated independently, rating symmetry (winner gain ≈ loser loss given bias).
- **Ingest:** integration tests — POST a report → correct rating deltas persisted;
  duplicate `matchId` is a no-op; malformed report rejected; transaction rolls back
  on failure.
- **Read API:** leaderboard ordering, profile aggregation, pagination.
- **Live hub:** a single smoke test that a posted match emits a leaderboard update.

---

## 10. Build order (within this sub-project)

1. `Elo` engine (pure, TDD).
2. Domain entities + EF Core + initial migration (PostgreSQL).
3. Ingest API (`POST /api/matches`) + host-token auth + idempotency + transaction.
4. Read API (leaderboard, profile, match detail).
5. SignalR live hub + optional live-event ingest.

---

## 11. Open questions (carry into planning)

1. **Friend-code readability** (§7) — confirmed when building the mod.
2. **Starting `p0`** per map+settings before 100 local matches exist — use a sane
   default (0.30) and document it as tunable.
3. **Live transport from the mod** — REST per-event vs a SignalR connection from the
   mod. Default REST; revisit at plan time.
4. **Settings granularity** — how finely `settingsHash` groups rulesets for the
   base-rate (impostor count + map at minimum).
```
