using System.Text.Json.Serialization;
using Api;
using Api.Authentication;
using Application;
using FastEndpoints;
using FastEndpoints.Swagger;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddServices()
    .AddInfrastructure(builder)
    .AddPortalAuthentication(builder);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();

builder.Services.AddFastEndpoints();

if (builder.Environment.IsDevelopment())
{
    builder.Services.SwaggerDocument(options => options.MaxEndpointVersion = 1);
}

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseCors(AuthenticationExtensions.CorsPolicy);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(config =>
{
    // IndicateErrorCode puts the validator's error code on the wire as the frontend's i18n key.
    config.Errors.UseProblemDetails(problem => problem.IndicateErrorCode = true);
    config.Endpoints.RoutePrefix = "api";
    config.Versioning.Prefix = "v";
    config.Versioning.PrependToRoute = true;
    config.Versioning.DefaultVersion = 1;
    config.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
});

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

app.MapHealthChecks("/health").AllowAnonymous();

await app.RunAsync();

namespace Api
{
    public partial class Program { }
}
