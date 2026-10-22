using Api.Extensions;
using Application.Common;
using Application.UpdatePlanItem;
using FastEndpoints;

namespace Api.Endpoints.PlanItems;

public sealed class UpdatePlanItemEndpoint(UpdatePlanItem handler) : Endpoint<UpdatePlanItemRequest, PlanItemDto>
{
    public override void Configure()
    {
        Put("plan-items/{id}");
        Version(1);
        Summary(s => s.Summary = "Edit, move, complete or reopen a planned item");
    }

    public override async Task HandleAsync(UpdatePlanItemRequest req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);

        if (await this.SendIfFailedAsync(result))
        {
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
