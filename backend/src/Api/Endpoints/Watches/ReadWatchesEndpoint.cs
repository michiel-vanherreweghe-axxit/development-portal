using Application.Common;
using Application.ReadWatches;
using FastEndpoints;

namespace Api.Endpoints.Watches;

public sealed class ReadWatchesEndpoint(ReadWatches handler) : EndpointWithoutRequest<WatchesResponse>
{
    public override void Configure()
    {
        Get("watches");
        Version(1);
        Summary(s => s.Summary = "Read the watched workflows of the signed-in user");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await handler.ExecuteAsync(ct);

        await Send.OkAsync(response, ct);
    }
}
