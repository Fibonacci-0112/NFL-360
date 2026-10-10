# NFL 360 Coverage

NFL standings with every official tiebreaker applied, playoff probabilities from simulated seasons, and a
what-if simulator that runs in the browser. Built with .NET 11, Blazor (Server + WebAssembly), PostgreSQL and Aspire.

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
    Ingestion     nflverse importer + ESPN live poller behind anti-corruption layers (schema `ingestion`)
  NflCoverage.Web            Blazor host: static SSR pages, minimal APIs, module composition
  NflCoverage.Web.Client     WebAssembly: what-if simulator, chart component
  NflCoverage.AppHost        Aspire orchestration (PostgreSQL + web)
```

Some decisions, briefly:

- **Clean Architecture where it pays.** The real domain logic (tiebreakers, simulation) lives in dependency-free
  libraries. Read-heavy modules are thin query services over EF Core without repositories.
- **Render modes per page.** Standings, odds and team pages are static SSR with streaming. The live scoreboard is
  an Interactive Server island. The what-if simulator is Interactive WebAssembly and runs the *same* simulation
  engine as the server, so trying scenarios costs the server nothing.
- **Swappable data providers.** Providers map into a canonical model. Team ids are current franchise codes
  (OAK→LV, SD→LAC, STL→LA). An `ingestion.external_ids` crosswalk links provider ids to ours, so you can
  add a paid feed later by writing another adapter.
- **Events.** `League` raises `GameResultsChanged`; `Standings` invalidates its cache and `PlayoffOdds`
  recalculates. Handlers recompute idempotently, so an in-process bus is enough (no outbox).

## Data sources

- [nflverse](https://github.com/nflverse/nflverse-data): schedules and results 1999–present (`games.csv`), team
  colors and logos. Re-imported on startup and every 6 hours.
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

On first start, migrations run, the full nflverse schedule is imported, and the current season's weekly odds
history is computed in the background, which takes about a minute.

## Tests

```bash
dotnet test --solution NflCoverage.slnx
```

- `Rules.Tests` replays **every season from 2002 to 2025** from regular-season scores. It asserts that the
  computed playoff field, wild-card pairings, byes and divisional matchups match what actually happened.
- `Simulation.Tests`: determinism, probability invariants (7 playoff teams and 4 division winners per
  conference), what-if forcing, Elo behavior.
- `ArchitectureTests`: module boundaries and that the domain engines stay framework-free.
- `IntegrationTests`: imports a 2023 nflverse fixture through the real pipeline into PostgreSQL, then checks the
  APIs and pages. They use Testcontainers, or set `NFL_TEST_POSTGRES` to an admin connection string to reuse a
  running server.

## Known limitations

- The "net touchdowns" tiebreaker is not modeled; nflverse schedule data has no touchdown counts. A tie that
  gets that far falls through to a coin toss, which is flagged in the tiebreak notes.
- Standings and odds cover 2002 onward, when the current 32-team, 8-division alignment began.
- The playoff odds treat the regular season only. Once the playoffs start, conference and Super Bowl odds
  still come from Elo rather than from actual playoff results.
- Monte Carlo 100% is not the same as mathematically clinched. Official clinch and elimination markers are
  future work.
- The browser simulator runs on the .NET WebAssembly interpreter (about 2,000 runs per scenario). Enabling
  WebAssembly AOT would make it several times faster.
