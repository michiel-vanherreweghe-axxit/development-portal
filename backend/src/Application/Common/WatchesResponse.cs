namespace Application.Common;

// Read and update answer with the same shape, so they share one response.
public sealed record WatchesResponse
{
    public int RunLimit { get; init; }

    public List<WatchDto> Watches { get; init; } = [];
}
