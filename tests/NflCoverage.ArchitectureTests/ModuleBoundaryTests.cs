using System.Reflection;

namespace NflCoverage.ArchitectureTests;

/// <summary>
/// Enforces the modular-monolith boundaries on the compiled assemblies. The compiler only records references that
/// are actually used, so these checks reflect real dependencies rather than project-file intentions.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly[] Assemblies = LoadSolutionAssemblies();

    public static TheoryData<string> ModuleImplementations =>
        [.. Assemblies.Select(a => a.GetName().Name!).Where(n => n.StartsWith("NflCoverage.Modules.", StringComparison.Ordinal) && !n.EndsWith(".Contracts", StringComparison.Ordinal))];

    [Theory]
    [MemberData(nameof(ModuleImplementations))]
    public void Modules_only_depend_on_other_modules_through_their_contracts(string module)
    {
        var offending = References(module)
            .Where(r => r.StartsWith("NflCoverage.Modules.", StringComparison.Ordinal) && !r.EndsWith(".Contracts", StringComparison.Ordinal));

        offending.Should().BeEmpty($"{module} must reference other modules only via *.Contracts");
    }

    [Theory]
    [InlineData("NflCoverage.Rules")]
    [InlineData("NflCoverage.Simulation")]
    public void Domain_engines_stay_free_of_web_and_data_frameworks(string assembly)
    {
        References(assembly).Should().NotContain(r =>
            r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || r.StartsWith("Npgsql", StringComparison.Ordinal)
            || r.StartsWith("NflCoverage.Modules", StringComparison.Ordinal));
    }

    [Fact]
    public void WebAssembly_client_never_references_server_modules()
    {
        References("NflCoverage.Web.Client").Should().NotContain(r =>
            r.StartsWith("NflCoverage.Modules", StringComparison.Ordinal)
            || r == "NflCoverage.SharedKernel"
            || r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Contracts_do_not_depend_on_module_implementations()
    {
        foreach (var contracts in Assemblies.Select(a => a.GetName().Name!).Where(n => n.EndsWith(".Contracts", StringComparison.Ordinal)))
        {
            References(contracts).Should().NotContain(
                r => r.StartsWith("NflCoverage.Modules.", StringComparison.Ordinal) && !r.EndsWith(".Contracts", StringComparison.Ordinal),
                contracts);
        }
    }

    private static IEnumerable<string> References(string assembly) =>
        Assemblies.Single(a => a.GetName().Name == assembly).GetReferencedAssemblies().Select(r => r.Name!);

    private static Assembly[] LoadSolutionAssemblies() =>
    [
        .. Directory.GetFiles(AppContext.BaseDirectory, "NflCoverage.*.dll")
            .Where(f => !Path.GetFileName(f).Contains("Tests", StringComparison.Ordinal))
            .Select(Assembly.LoadFrom),
    ];
}
