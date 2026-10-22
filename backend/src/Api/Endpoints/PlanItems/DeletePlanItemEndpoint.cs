using Api.Extensions;
using Application.DeletePlanItem;
using FastEndpoints;

namespace Api.Endpoints.PlanItems;

public sealed record DeletePlanItemRequest
{
    public int Id { get; init; }
}

public sealed class DeletePlanItemEndpoint(DeletePlanItem handler) : Endpoint<DeletePlanItemRequest>
{
    public override void Configure()
    {
        Delete("plan-items/{id}");
        Version(1);
        Summary(s => s.Summary = "Remove an item from the plan; a GitHub issue itself is untouched");
    }

    public override async Task HandleAsync(DeletePlanItemRequest req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req.Id, ct);

        if (await this.SendIfFailedAsync(result))
        {
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
