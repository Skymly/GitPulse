namespace GitPulse.Tests.TestHelpers;

/// <summary>Holds one HTTP response in the handler, including after transport cancellation.</summary>
internal sealed class GatedRequestHandler(HttpMessageHandler inner, string path, int occurrence = 1) : DelegatingHandler(inner)
{
    private int _matchingRequests;
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var hold = request.RequestUri?.AbsolutePath == path &&
            Interlocked.Increment(ref _matchingRequests) == occurrence;
        var response = await base.SendAsync(request, ct);
        if (!hold)
            return response;

        using var registration = ct.Register(() => Canceled.TrySetResult());
        Started.TrySetResult();
        await Release.Task;
        Exited.TrySetResult();
        return response;
    }
}
