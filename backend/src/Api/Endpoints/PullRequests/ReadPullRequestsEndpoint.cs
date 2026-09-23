using Application.Common;
using Application.ReadPullRequests;
using FastEndpoints;

namespace Api.Endpoints.PullRequests;

public sealed class ReadPullRequestsEndpoint(ReadPullRequests handler) : EndpointWithoutRequest<List<PullRequestDto>>
{
    public override void Configure()
    {
        Get("pull-requests");
        Version(1);
        Summary(s =>
        {
            s.Summary = "Read the open pull requests across every watched repository";
            s.Description = "Each pull request carries its check runs and the collapsed review verdict.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await handler.ExecuteAsync(ct);

        await Send.OkAsync(response, ct);
    }
}
