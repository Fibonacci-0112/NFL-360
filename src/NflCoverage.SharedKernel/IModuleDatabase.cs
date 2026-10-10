namespace NflCoverage.SharedKernel;

/// <summary>A module's own schema. The host applies every module's migrations at startup when configured to.</summary>
public interface IModuleDatabase
{
    string Schema { get; }

    Task MigrateAsync(CancellationToken cancellationToken);
}
