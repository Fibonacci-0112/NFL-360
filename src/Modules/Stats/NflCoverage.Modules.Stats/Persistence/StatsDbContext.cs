using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NflCoverage.Modules.Stats.Contracts;

namespace NflCoverage.Modules.Stats.Persistence;

internal sealed class Player
{
    public required string Id { get; init; }

    public required string Name { get; set; }

    public string? Position { get; set; }

    public string? PositionGroup { get; set; }

    public string? HeadshotUrl { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? College { get; set; }

    public int? DraftYear { get; set; }

    public int? DraftRound { get; set; }

    public int? DraftPick { get; set; }

    public string? DraftTeam { get; set; }

    public int? RookieSeason { get; set; }

    public int? LastSeason { get; set; }

    public string? LatestTeam { get; set; }

    /// <summary>Source columns without a typed field, as a JSON object.</summary>
    public string Extra { get; set; } = "{}";
}

internal sealed class PlayerSeason
{
    public required string PlayerId { get; init; }

    public int Season { get; init; }

    public SeasonType SeasonType { get; init; }

    public string? Team { get; init; }

    public int Games { get; init; }

    public required StatLine Stats { get; init; }

    public string Extra { get; init; } = "{}";
}

internal sealed class PlayerGame
{
    public required string PlayerId { get; init; }

    public required string GameId { get; init; }

    public int Season { get; init; }

    public int Week { get; init; }

    public SeasonType SeasonType { get; init; }

    public string? Team { get; init; }

    public string? Opponent { get; init; }

    public required StatLine Stats { get; init; }

    public string Extra { get; init; } = "{}";
}

internal sealed class TeamSeason
{
    public required string Team { get; init; }

    public int Season { get; init; }

    public SeasonType SeasonType { get; init; }

    public int Games { get; init; }

    public required TeamOffense Offense { get; init; }

    public required TeamDefense Defense { get; init; }

    public int Penalties { get; init; }

    public int PenaltyYards { get; init; }

    public string Extra { get; init; } = "{}";
}

internal sealed class TeamGame
{
    public required string Team { get; init; }

    public required string GameId { get; init; }

    public int Season { get; init; }

    public int Week { get; init; }

    public SeasonType SeasonType { get; init; }

    public required string Opponent { get; init; }

    public required TeamOffense Offense { get; init; }

    public string Extra { get; init; } = "{}";
}

/// <summary>
/// Box-score statistics. The curated columns are typed (stored as EF complex types, flattened into columns);
/// everything else from the source is kept in a <c>jsonb</c> column.
/// </summary>
internal sealed class StatsDbContext(DbContextOptions<StatsDbContext> options) : DbContext(options)
{
    public const string Schema = "stats";

    public DbSet<Player> Players => Set<Player>();

    public DbSet<PlayerSeason> PlayerSeasons => Set<PlayerSeason>();

    public DbSet<PlayerGame> PlayerGames => Set<PlayerGame>();

    public DbSet<TeamSeason> TeamSeasons => Set<TeamSeason>();

    public DbSet<TeamGame> TeamGames => Set<TeamGame>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Player>(player =>
        {
            player.Property(p => p.Id).HasMaxLength(16);
            player.Property(p => p.Position).HasMaxLength(8);
            player.Property(p => p.PositionGroup).HasMaxLength(8);
            player.Property(p => p.DraftTeam).HasMaxLength(4);
            player.Property(p => p.LatestTeam).HasMaxLength(4);
            player.Property(p => p.Extra).HasColumnType("jsonb");
            player.HasIndex(p => p.Name);
        });

        modelBuilder.Entity<PlayerSeason>(row =>
        {
            row.HasKey(r => new { r.PlayerId, r.Season, r.SeasonType });
            row.Property(r => r.PlayerId).HasMaxLength(16);
            row.Property(r => r.SeasonType).HasConversion<string>().HasMaxLength(10);
            row.Property(r => r.Team).HasMaxLength(4);
            row.Property(r => r.Extra).HasColumnType("jsonb");
            row.ComplexProperty(r => r.Stats, MapStatLine);
            row.HasIndex(r => new { r.Season, r.SeasonType });
            row.HasIndex(r => new { r.Team, r.Season });
        });

        modelBuilder.Entity<PlayerGame>(row =>
        {
            row.HasKey(r => new { r.PlayerId, r.GameId });
            row.Property(r => r.PlayerId).HasMaxLength(16);
            row.Property(r => r.GameId).HasMaxLength(24);
            row.Property(r => r.SeasonType).HasConversion<string>().HasMaxLength(10);
            row.Property(r => r.Team).HasMaxLength(4);
            row.Property(r => r.Opponent).HasMaxLength(4);
            row.Property(r => r.Extra).HasColumnType("jsonb");
            row.ComplexProperty(r => r.Stats, MapStatLine);
            row.HasIndex(r => r.Season);
        });

        modelBuilder.Entity<TeamSeason>(row =>
        {
            row.HasKey(r => new { r.Team, r.Season, r.SeasonType });
            row.Property(r => r.Team).HasMaxLength(4);
            row.Property(r => r.SeasonType).HasConversion<string>().HasMaxLength(10);
            row.Property(r => r.Extra).HasColumnType("jsonb");
            row.ComplexProperty(r => r.Offense);
            row.ComplexProperty(r => r.Defense);
            row.HasIndex(r => new { r.Season, r.SeasonType });
        });

        modelBuilder.Entity<TeamGame>(row =>
        {
            row.HasKey(r => new { r.Team, r.GameId });
            row.Property(r => r.Team).HasMaxLength(4);
            row.Property(r => r.GameId).HasMaxLength(24);
            row.Property(r => r.Opponent).HasMaxLength(4);
            row.Property(r => r.SeasonType).HasConversion<string>().HasMaxLength(10);
            row.Property(r => r.Extra).HasColumnType("jsonb");
            row.ComplexProperty(r => r.Offense);
            row.HasIndex(r => new { r.Season, r.SeasonType });
        });
    }

    private static void MapStatLine(ComplexPropertyBuilder<StatLine> line)
    {
        line.ComplexProperty(s => s.Passing);
        line.ComplexProperty(s => s.Rushing);
        line.ComplexProperty(s => s.Receiving);
        line.ComplexProperty(s => s.Defense);
        line.ComplexProperty(s => s.Kicking);
    }
}

/// <summary>Lets <c>dotnet ef</c> build the model without the web host.</summary>
internal sealed class StatsDbContextDesignFactory : IDesignTimeDbContextFactory<StatsDbContext>
{
    public StatsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<StatsDbContext>()
            .UseNpgsql("Host=localhost;Database=nfl", o => o.MigrationsHistoryTable("__EFMigrationsHistory", StatsDbContext.Schema))
            .Options);
}
