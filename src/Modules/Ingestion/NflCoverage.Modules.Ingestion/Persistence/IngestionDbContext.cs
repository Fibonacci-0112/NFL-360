using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NflCoverage.Modules.Ingestion.Persistence;

/// <summary>Maps a provider's identifier to our canonical id, so providers can be swapped or combined.</summary>
internal sealed class ExternalId
{
    public required string Provider { get; init; }

    public required string EntityType { get; init; }

    public required string ExternalKey { get; init; }

    public required string InternalId { get; set; }
}

internal sealed class ImportRun
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Source { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; set; }

    public int RowsRead { get; set; }

    public int RowsChanged { get; set; }

    public string? Error { get; set; }
}

internal sealed class IngestionDbContext(DbContextOptions<IngestionDbContext> options) : DbContext(options)
{
    public const string Schema = "ingestion";

    public DbSet<ExternalId> ExternalIds => Set<ExternalId>();

    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<ExternalId>(e =>
        {
            e.HasKey(x => new { x.Provider, x.EntityType, x.ExternalKey });
            e.Property(x => x.Provider).HasMaxLength(32);
            e.Property(x => x.EntityType).HasMaxLength(32);
            e.Property(x => x.ExternalKey).HasMaxLength(64);
            e.Property(x => x.InternalId).HasMaxLength(64);
            e.HasIndex(x => new { x.Provider, x.EntityType, x.InternalId });
        });
        modelBuilder.Entity<ImportRun>(e =>
        {
            e.Property(x => x.Source).HasMaxLength(32);
            e.HasIndex(x => x.StartedAt);
        });
    }
}

internal sealed class IngestionDbContextDesignFactory : IDesignTimeDbContextFactory<IngestionDbContext>
{
    public IngestionDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IngestionDbContext>()
            .UseNpgsql("Host=localhost;Database=nfl", o => o.MigrationsHistoryTable("__EFMigrationsHistory", IngestionDbContext.Schema))
            .Options);
}
