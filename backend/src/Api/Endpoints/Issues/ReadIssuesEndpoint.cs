using Api.Extensions;
using Application.Common;
using Application.ReadIssues;
using FastEndpoints;

namespace Api.Endpoints.Issues;

public sealed class ReadIssuesEndpoint(ReadIssues handler) : Endpoint<ReadIssuesRequest, List<IssueDto>>
{
    public override void Configure()
    {
        Get("issues");
        Version(1);
        Summary(s => s.Summary = "Search the issues of one repository the signed-in user can see");
    }

    public override async Task HandleAsync(ReadIssuesRequest req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);

        if (await this.SendIfFailedAsync(result))
        {
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
