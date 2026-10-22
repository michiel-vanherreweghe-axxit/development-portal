using Application.Common;
using Application.ReadPlanItems;
using FastEndpoints;

namespace Api.Endpoints.PlanItems;

public sealed class ReadPlanItemsEndpoint(ReadPlanItems handler) : Endpoint<ReadPlanItemsRequest, List<PlanItemDto>>
{
    public override void Configure()
    {
        Get("plan-items");
        Version(1);
        Summary(s => s.Summary = "Read the planned items of the signed-in user between two dates");
    }

    public override async Task HandleAsync(ReadPlanItemsRequest req, CancellationToken ct)
    {
        var response = await handler.ExecuteAsync(req, ct);

        await Send.OkAsync(response, ct);
    }
}
