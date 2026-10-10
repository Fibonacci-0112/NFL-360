using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NflCoverage.Modules.Ingestion.Espn;
using NflCoverage.Modules.Ingestion.Nflverse;
using NflCoverage.Modules.Ingestion.Persistence;
using NflCoverage.SharedKernel;

namespace NflCoverage.Modules.Ingestion;

public sealed class IngestionModule : IModule
{
    public string Name => "Ingestion";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(IngestionOptions.Section);
        services.Configure<IngestionOptions>(section);
        services.AddDbContext<IngestionDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("nfl"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IngestionDbContext.Schema)));
        services.AddScoped<IModuleDatabase, IngestionDatabase>();

        services.AddHttpClient<NflverseImporter>(c => c.Timeout = TimeSpan.FromMinutes(2)).AddStandardResilienceHandler();
        services.AddHttpClient(EspnLivePoller.HttpClientName).AddStandardResilienceHandler();

        if (section.GetValue(nameof(IngestionOptions.Enabled), defaultValue: true))
        {
            services.AddHostedService<NflverseSyncService>();
            services.AddHostedService<EspnLivePoller>();
        }
    }

    private sealed class IngestionDatabase(IngestionDbContext db) : IModuleDatabase
    {
        public string Schema => IngestionDbContext.Schema;

        public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
    }
}
