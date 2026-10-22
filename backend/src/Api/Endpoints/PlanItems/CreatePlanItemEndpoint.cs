using Api.Extensions;
using Application.Common;
using Application.CreatePlanItem;
using FastEndpoints;

namespace Api.Endpoints.PlanItems;

public sealed class CreatePlanItemEndpoint(CreatePlanItem handler) : Endpoint<CreatePlanItemRequest, PlanItemDto>
{
    public override void Configure()
    {
        Post("plan-items");
        Version(1);
        Summary(s => s.Summary = "Add a to-do or a GitHub issue to the plan of the signed-in user");
    }

    public override async Task HandleAsync(CreatePlanItemRequest req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);

        if (await this.SendIfFailedAsync(result))
        {
            return;
        }

        await Send.ResponseAsync(result.Value, StatusCodes.Status201Created, ct);
    }
}
