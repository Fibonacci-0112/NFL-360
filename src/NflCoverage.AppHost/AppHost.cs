// Local orchestration: PostgreSQL (with pgweb) plus the web app, wired together with OpenTelemetry in the Aspire dashboard.
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgWeb();
var database = postgres.AddDatabase("nfl");

builder.AddProject<Projects.NflCoverage_Web>("web")
    .WithReference(database)
    .WaitFor(database)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
