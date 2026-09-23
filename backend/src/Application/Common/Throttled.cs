namespace Application.Common;

public static class Throttled
{
    // GitHub is called once per watch and twice per pull request, so the fan-out is capped
    // rather than let loose on the rate limit.
    public static async Task<IReadOnlyList<TResult>> MapAsync<TSource, TResult>(
        IReadOnlyCollection<TSource> source,
        int maxParallel,
        Func<TSource, Task<TResult>> map)
    {
        using var gate = new SemaphoreSlim(maxParallel);

        var results = await Task.WhenAll(source.Select(async item =>
        {
            await gate.WaitAsync();

            try
            {
                return await map(item);
            }
            finally
            {
                gate.Release();
            }
        }));

        return results;
    }
}
