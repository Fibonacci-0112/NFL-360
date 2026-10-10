using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NflCoverage.Modules.PlayoffOdds.Persistence;

internal sealed class OddsRun
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public int Season { get; init; }

    public int ThroughWeek { get; init; }

    public int Iterations { get; init; }

    public DateTimeOffset ComputedAt { get; init; }

    public List<TeamOdds> Teams { get; init; } = [];
}

internal sealed class TeamOdds
{
    public Guid RunId { get; init; }

    public required string TeamId { get; init; }

    public double MeanWins { get; init; }

    public double MakePlayoffs { get; init; }

    public double WinDivision { get; init; }

    public double FirstSeed { get; init; }

    public required double[] SeedProbabilities { get; init; }

    public double WinConference { get; init; }

    public double WinSuperBowl { get; init; }
}

internal sealed class OddsDbContext(DbContextOptions<OddsDbContext> options) : DbContext(options)
{
    public const string Schema = "odds";

    public DbSet<OddsRun> Runs => Set<OddsRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<OddsRun>(run =>
        {
            // One snapshot per season and week; recalculating replaces it.
            run.HasIndex(r => new { r.Season, r.ThroughWeek }).IsUnique();
            run.HasMany(r => r.Teams).WithOne().HasForeignKey(t => t.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TeamOdds>(team =>
        {
            team.HasKey(t => new { t.RunId, t.TeamId });
            team.Property(t => t.TeamId).HasMaxLength(4);
        });
    }
}

internal sealed class OddsDbContextDesignFactory : IDesignTimeDbContextFactory<OddsDbContext>
{
    public OddsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OddsDbContext>()
            .UseNpgsql("Host=localhost;Database=nfl", o => o.MigrationsHistoryTable("__EFMigrationsHistory", OddsDbContext.Schema))
            .Options);
}
