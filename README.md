# Among Us Ranked ("0.25 ranked")

A community ranked ladder for Among Us with a dual-Elo rating system
(Crew-Elo + Impostor-Elo → combined), built to include **mobile players with zero
install**.

## How it works

Ranked games run on **official Among Us servers** as a normal private lobby (6-char
code). One PC in the lobby runs the **Ranked Host mod**, which can read the full game
state for every player — including unmodded mobile/console players — because Among Us
is host-authoritative. The mod streams live events and POSTs an authoritative match
report to the backend, which computes ELO and updates the live leaderboard.

> Mobile (especially iOS) can't connect to custom servers, so staying on official
> servers + a join code is the only zero-setup way to include them. See the design
> spec for the full rationale.

## Components

| # | Component | Status | Spec |
|---|-----------|--------|------|
| 1 | **Backend + ELO engine** (C# / ASP.NET Core + SignalR + EF Core + PostgreSQL) | Design approved | [`docs/superpowers/specs/2026-06-26-backend-elo-design.md`](docs/superpowers/specs/2026-06-26-backend-elo-design.md) |
| 2 | **Ranked Host mod** (C# / BepInEx + Reactor) | Not started | TBD |
| 3 | **Website / leaderboard** (live via SignalR) | Not started | TBD |

Build order: backend + ELO first (testable with zero Among Us involved), then the
mod, then the website.

## Design docs

See [`docs/superpowers/specs/`](docs/superpowers/specs/).
