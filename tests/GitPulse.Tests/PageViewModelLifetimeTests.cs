using GitPulse.App.Views;
using Xunit;

namespace GitPulse.Tests;

public class PageViewModelLifetimeTests
{
    [Fact]
    public void ClearedWithoutEverBeingParented_DoesNotDispose()
    {
        var viewModel = new RecordingDisposable();
        var hadParent = false;
        var disposed = false;

        PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed);

        Assert.Equal(0, viewModel.DisposeCount);
        Assert.False(hadParent);
        Assert.False(disposed);
    }

    [Fact]
    public void ParentedThenCleared_DisposesViewModel()
    {
        var viewModel = new RecordingDisposable();
        var hadParent = false;
        var disposed = false;

        PageViewModelLifetime.OnParentSet(true, viewModel, ref hadParent, ref disposed);
        Assert.Equal(0, viewModel.DisposeCount);

        PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed);

        Assert.Equal(1, viewModel.DisposeCount);
        Assert.True(disposed);
    }

    [Fact]
    public void RepeatedlyParented_DoesNotDispose()
    {
        var viewModel = new RecordingDisposable();
        var hadParent = false;
        var disposed = false;

        PageViewModelLifetime.OnParentSet(true, viewModel, ref hadParent, ref disposed);
        PageViewModelLifetime.OnParentSet(true, viewModel, ref hadParent, ref disposed);
        PageViewModelLifetime.OnParentSet(true, viewModel, ref hadParent, ref disposed);

        Assert.Equal(0, viewModel.DisposeCount);
        Assert.True(hadParent);
        Assert.False(disposed);
    }

    [Fact]
    public void ClearedRepeatedlyAfterPop_DisposesOnlyOnce()
    {
        var viewModel = new RecordingDisposable();
        var hadParent = false;
        var disposed = false;

        PageViewModelLifetime.OnParentSet(true, viewModel, ref hadParent, ref disposed);
        PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed);
        PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed);

        Assert.Equal(1, viewModel.DisposeCount);
        Assert.True(hadParent);
        Assert.True(disposed);
    }

    [Fact]
    public void ReattachedAfterPop_DoesNotDisposeAgain()
    {
        var viewModel = new RecordingDisposable();
        var hadParent = false;
        var disposed = false;

        PageViewModelLifetime.OnParentSet(true, viewModel, ref hadParent, ref disposed);
        PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed);
        PageViewModelLifetime.OnParentSet(true, viewModel, ref hadParent, ref disposed);
        PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed);

        Assert.Equal(1, viewModel.DisposeCount);
    }

    [Fact]
    public void ThrowingExtraDispose_LeavesViewModelUndisposed_AndFlagStaysSet()
    {
        var viewModel = new RecordingDisposable();
        var hadParent = false;
        var disposed = false;

        PageViewModelLifetime.OnParentSet(true, viewModel, ref hadParent, ref disposed);
        Assert.Throws<InvalidOperationException>(() =>
            PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed,
                () => throw new InvalidOperationException()));

        Assert.Equal(0, viewModel.DisposeCount);
        Assert.True(disposed);

        PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed);

        Assert.Equal(0, viewModel.DisposeCount);
    }

    [Fact]
    public void ExtraDispose_RunsOnlyOnPop_BeforeViewModelDispose()
    {
        var order = new List<string>();
        var viewModel = new RecordingDisposable(order);
        var hadParent = false;
        var disposed = false;

        PageViewModelLifetime.OnParentSet(true, viewModel, ref hadParent, ref disposed, () => order.Add("extra"));
        PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed, () => order.Add("extra"));
        PageViewModelLifetime.OnParentSet(false, viewModel, ref hadParent, ref disposed, () => order.Add("extra"));

        Assert.Equal(new[] { "extra", "viewModel" }, order);
    }

    private sealed class RecordingDisposable : IDisposable
    {
        private readonly List<string>? _log;

        public RecordingDisposable(List<string>? log = null) => _log = log;

        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            _log?.Add("viewModel");
        }
    }
}
