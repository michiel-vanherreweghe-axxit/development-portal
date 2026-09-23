using Api.Extensions;
using Application.Common;
using Application.ReadRepositories;
using FastEndpoints;

namespace Api.Endpoints.Repositories;

public sealed class ReadRepositoriesEndpoint(ReadRepositories handler) : EndpointWithoutRequest<List<RepositoryDto>>
{
    public override void Configure()
    {
        Get("repositories");
        Version(1);
        Summary(s => s.Summary = "Read the repositories the signed-in user can see");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(ct);

        var resultFailed = await this.SendIfFailedAsync(result);
        if (resultFailed)
        {
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
