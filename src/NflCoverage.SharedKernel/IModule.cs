using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NflCoverage.SharedKernel;

/// <summary>
/// Entry point of a module in the modular monolith. The host discovers modules explicitly and lets each one
/// register its own services and HTTP endpoints; nothing else of a module is visible to the host.
/// </summary>
public interface IModule
{
    string Name { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
