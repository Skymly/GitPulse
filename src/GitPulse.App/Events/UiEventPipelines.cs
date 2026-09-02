using Observables.Events.R3;
using R3;

namespace GitPulse.App.Events;

/// <summary>
/// Observable text source for ADR-007. MAUI <c>SearchBar.Events()</c> hits CS0122
/// (<c>IControlsVisualElement</c>); this adapter is a public-event type
/// Observables.Events.R3 can generate for.
/// </summary>
internal sealed class SearchTextSource
{
    public event Action<string>? TextChanged;

    public void Publish(string text) => TextChanged?.Invoke(text);
}

/// <summary>
/// Observable load-more source. CollectionView remaining-items is bridged here
/// so the pipeline is <c>.Events()</c> rather than a code-behind click handler.
/// </summary>
internal sealed class LoadMoreSource
{
    public event Action? Requested;

    public void Request() => Requested?.Invoke();
}

/// <summary>
/// Shared Observables.Events.R3 pipelines used by Repos and Search.
/// </summary>
internal static class UiEventPipelines
{
    public const int SearchDebounceMs = 300;

    public static IDisposable BindSearchText(
        SearchBar searchBar,
        BindableReactiveProperty<string> target)
    {
        var source = new SearchTextSource();
        EventHandler<TextChangedEventArgs> handler = (_, e) =>
            source.Publish(e.NewTextValue ?? string.Empty);
        searchBar.TextChanged += handler;

        var subscription = source.Events().TextChanged
            .Debounce(TimeSpan.FromMilliseconds(SearchDebounceMs), TimeProvider.System)
            .DistinctUntilChanged()
            .ObserveOnCurrentSynchronizationContext()
            .Subscribe(text =>
            {
                if (target.Value != text)
                    target.Value = text;
            });

        return Disposable.Create(() =>
        {
            searchBar.TextChanged -= handler;
            subscription.Dispose();
        });
    }

    public static IDisposable BindLoadMore(
        CollectionView list,
        BindableReactiveProperty<bool> canLoadMore,
        Func<Task> loadMore)
    {
        var source = new LoadMoreSource();
        EventHandler handler = (_, _) => source.Request();
        list.RemainingItemsThresholdReached += handler;

        var subscription = source.Events().Requested
            .ObserveOnCurrentSynchronizationContext()
            .SubscribeAwait(async (_, _) =>
            {
                if (canLoadMore.Value)
                    await loadMore();
            });

        return Disposable.Create(() =>
        {
            list.RemainingItemsThresholdReached -= handler;
            subscription.Dispose();
        });
    }
}
