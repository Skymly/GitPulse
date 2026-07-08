using GitPulse.ViewModels;
using GitPulse.Core.Models;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// Repos page code-behind — demonstrates reactive event bridging for the
/// <see cref="SearchBar"/>.TextChanged event using R3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observables.Events.R3 0.1.4 + MAUI limitation:</b> The source-generated
/// <c>.Events()</c> extension for <c>Microsoft.Maui.Controls.SearchBar</c>
/// produces code that references <c>IControlsVisualElement</c>, an internal
/// MAUI interface, causing CS0122. Until the upstream generator handles MAUI's
/// internal accessibility, we bridge the event manually via an R3
/// <see cref="Subject{T}"/> — still fully reactive, just not source-generated
/// for this specific control. The Events domain is still showcased on
/// simpler types that don't hit this issue.
/// </para>
/// <para>
/// <b>R3 Throttle note:</b> R3 1.3.0 time-based operators require an explicit
/// <see cref="TimeProvider"/>. <see cref="TimeProvider.System"/> is used here;
/// with <c>UseR3()</c> the MAUI dispatcher TimeProvider is the global default
/// for other operators.
/// </para>
/// </remarks>
public partial class ReposPage : ContentPage
{
    private readonly ReposViewModel _viewModel;
    private readonly Subject<string> _searchSubject = new();
    private IDisposable? _searchSubscription;
    private bool _loaded;

    public ReposPage(ReposViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // Manual event → Observable bridge (workaround for the
        // Observables.Events.R3 + MAUI internal interface issue).
        SearchBar.TextChanged += OnSearchBarTextChanged;

        // Reactive pipeline: debounce + distinct + filter.
        _searchSubscription = _searchSubject
            .Debounce(TimeSpan.FromMilliseconds(300), TimeProvider.System)
            .DistinctUntilChanged()
            .ObserveOnCurrentSynchronizationContext()
            .Subscribe(text =>
            {
                if (_viewModel.SearchText.Value != text)
                    _viewModel.SearchText.Value = text;
            });
    }

    private void OnSearchBarTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchSubject.OnNext(e.NewTextValue ?? string.Empty);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_loaded)
        {
            _loaded = true;
            _ = _viewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _searchSubscription?.Dispose();
        _searchSubscription = null;
        _searchSubject.Dispose();
        SearchBar.TextChanged -= OnSearchBarTextChanged;
        // ViewModels are transient; dispose to release R3 subscriptions.
        _viewModel.Dispose();
    }

    private async void OnRepoSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Repo repo)
        {
            // Deselect so the same item can be re-tapped later.
            ((CollectionView)sender!).SelectedItem = null;

            // Parse owner/repo from FullName (format: "owner/repo").
            var parts = repo.FullName.Split('/', 2);
            if (parts.Length == 2)
            {
                await Shell.Current.GoToAsync(
                    $"RepoDetailPage?owner={Uri.EscapeDataString(parts[0])}" +
                    $"&repo={Uri.EscapeDataString(parts[1])}");
            }
        }
    }
}
