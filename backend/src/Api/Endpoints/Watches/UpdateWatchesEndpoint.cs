using Api.Extensions;
using Application.Common;
using Application.UpdateWatches;
using FastEndpoints;

namespace Api.Endpoints.Watches;

public sealed class UpdateWatchesEndpoint(UpdateWatches handler) : Endpoint<UpdateWatchesRequest, WatchesResponse>
{
    public override void Configure()
    {
        Post("watches");
        Version(1);
        Summary(s => s.Summary = "Replace the watched workflows of the signed-in user");
    }

    public override async Task HandleAsync(UpdateWatchesRequest req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);

        var resultFailed = await this.SendIfFailedAsync(result);
        if (resultFailed)
        {
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
