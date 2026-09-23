using Application.Common;
using Application.ReadRuns;
using FastEndpoints;

namespace Api.Endpoints.Runs;

public sealed class ReadRunsEndpoint(ReadRuns handler) : EndpointWithoutRequest<List<WatchRunsDto>>
{
    public override void Configure()
    {
        Get("runs");
        Version(1);
        Summary(s =>
        {
            s.Summary = "Read the latest runs of every watched workflow";
            s.Description = "A repository that cannot be read carries its error on its own row instead of failing the request.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await handler.ExecuteAsync(ct);

        await Send.OkAsync(response, ct);
    }
}
