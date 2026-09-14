using System.Runtime.CompilerServices;
using GitPulse.Core.Abstractions;

namespace GitPulse.ViewModels;

/// <summary>
/// Per-factory invalidation bus for credential changes. ViewModels keyed to
/// the same <see cref="IGitHubClientFactory"/> instance share a gate; tests
/// that construct a fresh factory stay isolated.
/// </summary>
internal static class CredentialEpoch
{
    private static readonly ConditionalWeakTable<IGitHubClientFactory, Gate> Gates = new();

    public static Gate For(IGitHubClientFactory factory) => Gates.GetOrCreateValue(factory);

    public static IDisposable Subscribe(IGitHubClientFactory factory, Action handler)
    {
        var gate = For(factory);
        gate.Invalidated += handler;
        return new Subscription(gate, handler);
    }

    internal sealed class Gate
    {
        public event Action? Invalidated;

        public void Invalidate() => Invalidated?.Invoke();
    }

    private sealed class Subscription(Gate gate, Action handler) : IDisposable
    {
        public void Dispose() => gate.Invalidated -= handler;
    }
}
