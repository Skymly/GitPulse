namespace GitPulse.Tests.TestHelpers;

/// <summary>Holds one HTTP response in the handler, including after transport cancellation.</summary>
internal sealed class GatedRequestHandler(HttpMessageHandler inner, int blockedRequest) : DelegatingHandler(inner)
{
    private int _requests;
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var number = Interlocked.Increment(ref _requests);
        var response = await base.SendAsync(request, ct);
        if (number != blockedRequest)
            return response;

        using var registration = ct.Register(() => Canceled.TrySetResult());
        Started.TrySetResult();
        await Release.Task;
        Exited.TrySetResult();
        return response;
    }
}
