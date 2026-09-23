using Api.Extensions;
using Application.Common;
using Application.ReadWorkflows;
using FastEndpoints;

namespace Api.Endpoints.Workflows;

public sealed class ReadWorkflowsEndpoint(ReadWorkflows handler) : Endpoint<ReadWorkflowsRequest, List<WorkflowDto>>
{
    public override void Configure()
    {
        Get("workflows");
        Version(1);
        Summary(s => s.Summary = "Read the workflows of one repository");
    }

    public override async Task HandleAsync(ReadWorkflowsRequest req, CancellationToken ct)
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
