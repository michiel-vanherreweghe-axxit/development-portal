using Api.Extensions;
using Application.Common;
using Application.MergePullRequest;
using FastEndpoints;

namespace Api.Endpoints.PullRequests;

public sealed class MergePullRequestEndpoint(MergePullRequest handler) : Endpoint<PullRequestRequest>
{
    public override void Configure()
    {
        Post("pull-requests/{owner}/{repository}/{number}/merge");
        Version(1);
        Summary(s =>
        {
            s.Summary = "Squash and merge a pull request that is ready to merge";
            s.Description =
                "Always a squash merge. Refused with a conflict when it is a draft, has changes requested, " +
                "or its checks are not green.";
        });
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
