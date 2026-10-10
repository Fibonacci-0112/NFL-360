using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NflCoverage.Modules.League.Contracts;

namespace NflCoverage.Modules.League.Persistence;

internal sealed class Team
{
    public required string Id { get; init; }

    public required string Name { get; set; }

    public required string Nickname { get; set; }

    public required string Conference { get; set; }

    public required string Division { get; set; }

    public required string PrimaryColor { get; set; }

    public required string SecondaryColor { get; set; }

    public string? LogoUrl { get; set; }
}

internal sealed class Game
{
    public required string Id { get; init; }

    public int Season { get; set; }

    public required string GameType { get; set; }

    public int Week { get; set; }

    public DateTimeOffset? Kickoff { get; set; }

    public required string HomeTeamId { get; set; }

    public required string AwayTeamId { get; set; }

    public int? HomeScore { get; set; }

    public int? AwayScore { get; set; }

    public GameStatus Status { get; set; }

    public string? StatusDetail { get; set; }

    public bool NeutralSite { get; set; }

    public double? HomeSpread { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class LeagueDbContext(DbContextOptions<LeagueDbContext> options) : DbContext(options)
{
    public const string Schema = "league";

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Team>(team =>
        {
            team.Property(t => t.Id).HasMaxLength(4);
            team.Property(t => t.Conference).HasMaxLength(3);
            team.Property(t => t.Division).HasMaxLength(16);
            team.Property(t => t.PrimaryColor).HasMaxLength(9);
            team.Property(t => t.SecondaryColor).HasMaxLength(9);
        });

        modelBuilder.Entity<Game>(game =>
        {
            game.Property(g => g.Id).HasMaxLength(24);
            game.Property(g => g.GameType).HasMaxLength(3);
            game.Property(g => g.Status).HasConversion<string>().HasMaxLength(12);
            game.HasOne<Team>().WithMany().HasForeignKey(g => g.HomeTeamId).OnDelete(DeleteBehavior.Restrict);
            game.HasOne<Team>().WithMany().HasForeignKey(g => g.AwayTeamId).OnDelete(DeleteBehavior.Restrict);
            game.HasIndex(g => new { g.Season, g.Week });
            game.HasIndex(g => g.Kickoff);
        });
    }
}

/// <summary>Lets <c>dotnet ef</c> build the model without the web host.</summary>
internal sealed class LeagueDbContextDesignFactory : IDesignTimeDbContextFactory<LeagueDbContext>
{
    public LeagueDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LeagueDbContext>()
            .UseNpgsql("Host=localhost;Database=nfl", o => o.MigrationsHistoryTable("__EFMigrationsHistory", LeagueDbContext.Schema))
            .Options);
}
