using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// The api trusts exactly one frontend origin, so the dev server keeps a fixed port instead
// of whatever happens to be free.
var frontend = builder.AddViteApp("frontend", "../../../frontend")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5173;
        endpoint.TargetPort = 5173;
        endpoint.IsProxied = false;
    })
    .WithExternalHttpEndpoints();

// The data volume is what makes it persistent: users, their watched workflows and run limits
// survive the container being recreated.
var database = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("portal");

var api = builder.AddProject<Api>("api")
    .WithReference(database)
    .WaitFor(database)
    // Without a launch profile the project carries no endpoint of its own, so the frontend
    // would have nothing to point at and Kestrel would fall back to its default port. The port
    // is fixed like the dev server's: the frontend resolves VITE_API_URL when it starts, which is
    // before the api — waiting on postgres — would have been handed a free one.
    .WithHttpEndpoint(port: 5174)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithEnvironment("Portal__FrontendUri", frontend.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

frontend.WithEnvironment("VITE_API_URL", api.GetEndpoint("http"));

builder.Build().Run();
