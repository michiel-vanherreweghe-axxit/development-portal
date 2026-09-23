using Api.Extensions;
using Application.ClosePullRequest;
using Application.Common;
using FastEndpoints;

namespace Api.Endpoints.PullRequests;

public sealed class ClosePullRequestEndpoint(ClosePullRequest handler) : Endpoint<PullRequestRequest>
{
    public override void Configure()
    {
        Post("pull-requests/{owner}/{repository}/{number}/close");
        Version(1);
        Summary(s => s.Summary = "Close a pull request without merging it");
    }

    public override async Task HandleAsync(PullRequestRequest req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);

        if (await this.SendIfFailedAsync(result))
        {
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
