using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NflCoverage.Modules.Ingestion.Nflverse;
using NflCoverage.Modules.Ingestion.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NflCoverage.IntegrationTests;

/// <summary>
/// Hosts the real app against a throwaway PostgreSQL database, with background ingestion and odds recalculation
/// switched off, then imports the 2023 nflverse fixtures (schedule and statistics) through the real import pipeline.
/// Set <c>NFL_TEST_POSTGRES</c> to reuse a running server; otherwise a Testcontainers PostgreSQL is started.
/// </summary>
public sealed class NflAppFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string _adminConnectionString = "";
    private string _databaseName = "";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("NFL_TEST_POSTGRES") ?? "";
        if (_adminConnectionString.Length == 0)
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
            _adminConnectionString = _container.GetConnectionString();
        }

        _databaseName = $"nfl_test_{Guid.NewGuid():N}";
        await ExecuteAdminAsync($"CREATE DATABASE {_databaseName}");
        var connectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName }.ConnectionString;

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Testing");
            web.UseSetting("ConnectionStrings:nfl", connectionString);
            web.UseSetting("Ingestion:Enabled", "false");
            web.UseSetting("PlayoffOdds:BackgroundRecalculation", "false");
            web.UseSetting("PlayoffOdds:Iterations", "800");
        });

        _ = Factory.Server; // starts the host, which applies migrations
        await ImportFixtureAsync();
        await ImportStatsFixtureAsync();
    }

    public async Task<int> ImportFixtureAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<NflverseImporter>();
        var db = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        var teams = NflverseParser.ParseTeams(new StringReader(await File.ReadAllTextAsync(DataPath("teams_colors_logos.csv"))));
        var games = NflverseParser.ParseGames(new StringReader(await File.ReadAllTextAsync(DataPath("games_2023.csv"))));
        var run = new ImportRun { Source = "test", StartedAt = DateTimeOffset.UtcNow };
        db.ImportRuns.Add(run);
        await importer.ImportAsync(teams, games, run, CancellationToken.None);
        await db.SaveChangesAsync();
        return run.RowsChanged;
    }

    /// <summary>Imports the trimmed 2023 statistics files, resolving nflverse release paths to <c>Data/stats</c>.</summary>
    /// <returns>The number of rows read.</returns>
    public async Task<int> ImportStatsFixtureAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<NflverseStatsImporter>();
        var db = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        var run = new ImportRun { Source = "test-stats", StartedAt = DateTimeOffset.UtcNow };
        db.ImportRuns.Add(run);
        await importer.ImportPlayersAsync(new StringReader(await File.ReadAllTextAsync(StatsDataPath("players.csv"))), run, CancellationToken.None);
        await importer.ImportSeasonAsync(2023, gameLogs: true, OpenStatsFixture, run, CancellationToken.None);
        await db.SaveChangesAsync();
        return run.RowsRead;
    }

    public static string DataPath(string file) => Path.Combine(AppContext.BaseDirectory, "Data", file);

    public static string StatsDataPath(string file) => Path.Combine(AppContext.BaseDirectory, "Data", "stats", file);

    private static async Task<TextReader?> OpenStatsFixture(string releasePath, CancellationToken cancellationToken)
    {
        var file = StatsDataPath(Path.GetFileName(releasePath));
        return File.Exists(file) ? new StringReader(await File.ReadAllTextAsync(file, cancellationToken)) : null;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE)");
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class NflAppGroup : ICollectionFixture<NflAppFixture>
{
    public const string Name = "NFL app";
}
