# Repository guidance

## Project

NFL 360 Coverage is a .NET 11 modular monolith built with Blazor, PostgreSQL, and Aspire. Its core standings/tiebreaker and season-simulation logic is shared between the server and the WebAssembly what-if simulator.

## Architecture

- Keep module implementations isolated: modules may depend on other modules only through their `*.Contracts` projects.
- Keep `NflCoverage.Rules` and `NflCoverage.Simulation` free of web, database, and module dependencies. These libraries must remain suitable for WebAssembly; `Simulation` may use `Rules`.
- Keep the WebAssembly client dependent only on shared pure libraries, not server modules or the shared kernel.
- Register modules explicitly in `src/NflCoverage.Web/Program.cs`. A module exposes service registration and, if needed, endpoint mapping through `IModule`.
- Preserve the architecture enforced by `tests/NflCoverage.ArchitectureTests/ModuleBoundaryTests.cs`.
- Use the existing module schema and persistence boundaries. Add cross-module APIs to the relevant `*.Contracts` project rather than referencing another module's implementation.

## Development

- The SDK is pinned in `global.json`; use the matching .NET 11 SDK.
- Restore, build, and test the solution with:

  ```bash
  dotnet restore NflCoverage.slnx
  dotnet build NflCoverage.slnx --no-restore
  dotnet test --solution NflCoverage.slnx
  ```

- Integration tests use PostgreSQL through Testcontainers and require Docker, or can use an admin connection string in `NFL_TEST_POSTGRES`.
- For local development, run `dotnet run --project src/NflCoverage.AppHost` with Docker, or run `src/NflCoverage.Web` against a configured PostgreSQL instance.
- Follow repository-wide compiler and analyzer settings in `Directory.Build.props`; warnings are treated as errors. Package versions are centrally managed in `Directory.Packages.props`.

## Tests and changes

- Add or update focused tests alongside changes. The historical rules suite validates seasons 2002–2025; simulation tests cover determinism, probability invariants, and what-if behavior.
- Run the solution tests for behavior or architecture changes. For database/API/page changes, run integration tests as well.
- Keep provider-specific data handling behind ingestion adapters and map it into the canonical domain model.
- Avoid unrelated changes and keep documentation in sync when changing commands or architecture.
