# NFL 360 Coverage

NFL standings with every official tiebreaker applied, playoff probabilities from simulated seasons, a what-if
simulator that runs in the browser, player and team statistics since 1999, and franchise history. Built with .NET 11,
Blazor (Server + WebAssembly), PostgreSQL and Aspire.

## Architecture

A **modular monolith**: one deployable app, split into modules that talk to each other only through their
`*.Contracts` projects. Architecture tests check this on the compiled assemblies.

```
src/
  NflCoverage.Rules          Pure C#: standings, NFL tiebreakers, playoff seeding (no dependencies, WASM-safe)
  NflCoverage.Simulation     Pure C#: Elo ratings + Monte Carlo season/playoff simulation (WASM-safe)
  NflCoverage.SharedKernel   IModule, in-process event bus
  Modules/
    League        Teams, seasons, games (schema `league`)
    Standings     Standings computed on demand from League results, cached per season (no storage)
    PlayoffOdds   Nightly/after-results simulations, weekly odds history (schema `odds`)
    Stats         Player and team box scores, leaderboards, EPA efficiency, game logs (schema `stats`)
    History       Franchise seasons, head-to-head series, champions; a cached read model over League (no storage)
    Ingestion     nflverse importers + ESPN live poller behind anti-corruption layers (schema `ingestion`)
  NflCoverage.Web            Blazor host: static SSR pages, minimal APIs, module composition
  NflCoverage.Web.Client     WebAssembly: what-if simulator, chart component
  NflCoverage.AppHost        Aspire orchestration (PostgreSQL + web)
```

Some decisions, briefly:

- **Clean Architecture where it pays.** The real domain logic (tiebreakers, simulation) lives in dependency-free
  libraries. Read-heavy modules are thin query services over EF Core without repositories.
- **Render modes per page.** Standings, odds, stats, history and team pages are static SSR with streaming. Tabs,
  season pickers and searches are plain links and GET forms, and the stats tables use QuickGrid's URL-based paging
  and sorting, so none of them needs a circuit. The live scoreboard is an Interactive Server island. The what-if simulator is Interactive WebAssembly and runs the *same* simulation
  engine as the server, so trying scenarios costs the server nothing.
- **Swappable data providers.** Providers map into a canonical model. Team ids are current franchise codes
  (OAK→LV, SD→LAC, STL→LA). An `ingestion.external_ids` crosswalk links provider ids to ours, so you can
  add a paid feed later by writing another adapter.
- **Events.** `League` raises `GameResultsChanged`; `Standings` and `History` invalidate their caches,
  `PlayoffOdds` recalculates, and `Ingestion` refreshes the current season's statistics once a week's last game is
  final. Handlers recompute idempotently, so an in-process bus is enough (no outbox).
- **Typed columns plus the rest.** Statistics keep a curated set of typed columns (EF Core complex types) for
  queries; every other source column goes into a `jsonb` column, so nothing from nflverse is dropped. Derived
  metrics (passer rating, EPA per play, yards per attempt) are pure functions in `StatFormulas`.

## Data sources

- [nflverse](https://github.com/nflverse/nflverse-data): schedules and results 1999–present (`games.csv`), team
  colors and logos. Re-imported on startup and every 6 hours.
- nflverse statistics releases: season player and team stats (`stats_player_*`, `stats_team_*`), team game stats
  with EPA (`stats_team_week_*`), player game logs (`stats_player_week_*`) and the player directory
  (`players.csv`). Missing seasons from 1999 on are backfilled once (about 35 MB, a minute or two); after that the
  current season is refreshed daily and again after each completed week. Game logs are kept for the two most recent
  seasons (`Ingestion:StatsGameLogSeasons`).
- ESPN's public scoreboard JSON (unofficial, undocumented): live scores, polled only around game windows.

## Running locally

Requirements: the .NET 11 SDK (version pinned in `global.json`), plus either Docker for Aspire or a local
PostgreSQL.

```bash
# With Aspire (starts PostgreSQL in Docker, opens the dashboard)
dotnet run --project src/NflCoverage.AppHost

# Or against your own PostgreSQL (see ConnectionStrings:nfl in appsettings.Development.json)
dotnet run --project src/NflCoverage.Web
```

On first start, migrations run, the full nflverse schedule is imported, the current season's weekly odds history is
computed, and statistics since 1999 are backfilled, all in the background; it takes a minute or two. Set
`Ingestion:StatsEnabled` to `false` to skip statistics.

## Tests

```bash
dotnet test --solution NflCoverage.slnx
```

- `Rules.Tests` replays **every season from 2002 to 2025** from regular-season scores. It asserts that the
  computed playoff field, wild-card pairings, byes and divisional matchups match what actually happened.
- `Simulation.Tests`: determinism, probability invariants (7 playoff teams and 4 division winners per
  conference), what-if forcing, Elo behavior.
- `Modules.Tests`: `StatFormulas` against official figures (2004 Peyton Manning's 121.1 passer rating, for one) and
  the History calculations on synthetic schedules (playoff round reached, streaks, series records).
- `ArchitectureTests`: module boundaries and that the domain engines stay framework-free.
- `IntegrationTests`: imports 2023 nflverse fixtures (schedule, trimmed player and team statistics) through the real
  pipeline into PostgreSQL, then checks the APIs against real 2023 figures (Tua Tagovailoa's 4,624 passing yards,
  head-to-head series, 32-team EPA comparison) and that every page renders. They use Testcontainers, or set `NFL_TEST_POSTGRES` to an admin connection string to reuse a
  running server.

## Known limitations

- The "net touchdowns" tiebreaker is not modeled; nflverse schedule data has no touchdown counts. A tie that
  gets that far falls through to a coin toss, which is flagged in the tiebreak notes.
- Standings and odds cover 2002 onward, when the current 32-team, 8-division alignment began.
- The playoff odds treat the regular season only. Once the playoffs start, conference and Super Bowl odds
  still come from Elo rather than from actual playoff results.
- Monte Carlo 100% is not the same as mathematically clinched. Official clinch and elimination markers are
  future work.
- Statistics come from nflverse's play-by-play aggregation and can differ slightly from official totals (tackles
  most of all). A player traded mid-season counts toward their latest team only in team leaderboards.
- History covers 1999 onward, the first season in the schedule data. Division titles and seeds are shown from 2002,
  when the current alignment began.
- The browser simulator runs on the .NET WebAssembly interpreter (about 2,000 runs per scenario). Enabling
  WebAssembly AOT would make it several times faster.
