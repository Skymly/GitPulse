using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Notifications view model — the M4 Events domain showcase.
/// Subscribes to <see cref="INotificationPoller.NotificationsUpdated"/>
/// and bridges the event to R3 <see cref="BindableReactiveProperty{T}"/>
/// for UI binding. Supports mark-as-read (single thread and all).
/// </summary>
/// <remarks>
/// <para>
/// <b>Events domain showcase:</b> The poller fires
/// <see cref="INotificationPoller.NotificationsUpdated"/> on each timer
/// tick. This ViewModel bridges that event to R3 reactive state:
/// <code>
/// poller.NotificationsUpdated += OnNotificationsUpdated;
/// // → OnNotificationsUpdated bridges to:
/// //   Notifications collection + UnreadCount reactive property
/// </code>
/// This demonstrates the reactive pipeline pattern: timer → HTTP →
/// event → R3 → UI, all without manual refresh.
/// </para>
/// </remarks>
public sealed partial class NotificationsViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly INotificationPoller _poller;
    private readonly IBrowserLauncher _browserLauncher;

    /// <summary>Notifications currently displayed.</summary>
    public ObservableCollection<Notification> Notifications { get; } = [];

    /// <summary>Unread notification count (for badge display).</summary>
    public BindableReactiveProperty<int> UnreadCount { get; } = new(0);

    /// <summary>Whether the poller is actively polling.</summary>
    public BindableReactiveProperty<bool> IsPolling { get; } = new(false);

    /// <summary>Whether a refresh or mark-as-read is in progress.</summary>
    public BindableReactiveProperty<bool> IsBusy { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>Whether the user is authenticated (has a stored token).</summary>
    public BindableReactiveProperty<bool> IsAuthenticated { get; } = new(false);

    public NotificationsViewModel(
        IGitHubClientFactory clientFactory,
        INotificationPoller poller,
        IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _poller = poller;
        _browserLauncher = browserLauncher;

        // Bridge poller events to R3 reactive state.
        _poller.NotificationsUpdated += OnNotificationsUpdated;
        _poller.IsPollingChanged += OnPollingChanged;

        // Check auth on construction.
        _ = CheckAuthAsync();
    }

    private async Task CheckAuthAsync()
    {
        var client = await _clientFactory.CreateClientAsync();
        IsAuthenticated.Value = client.DefaultRequestHeaders.Authorization is not null;
    }

    private void OnNotificationsUpdated(Notification[] notifications, int unreadCount)
    {
        Notifications.Clear();
        foreach (var n in notifications)
            Notifications.Add(n);
        UnreadCount.Value = unreadCount;
    }

    private void OnPollingChanged(bool isPolling)
    {
        IsPolling.Value = isPolling;
    }

    /// <summary>Start polling (called when the page appears).</summary>
    [RelayCommand]
    private void StartPolling()
    {
        _poller.Start();
    }

    /// <summary>Stop polling (called when the page disappears).</summary>
    [RelayCommand]
    private void StopPolling()
    {
        _poller.Stop();
    }

    /// <summary>Manual refresh — trigger an immediate poll.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy.Value = true;
        ErrorMessage.Value = string.Empty;
        try
        {
            await _poller.RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsBusy.Value = false;
        }
    }

    /// <summary>Mark a single notification thread as read.</summary>
    [RelayCommand]
    private async Task MarkAsReadAsync(Notification notification)
    {
        if (IsBusy.Value)
            return;

        IsBusy.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await api.MarkThreadRead(notification.Id).FirstAsync(cts.Token);

            // Remove from list and update count.
            Notifications.Remove(notification);
            if (UnreadCount.Value > 0)
                UnreadCount.Value--;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Mark as read failed: {ex.Message}";
        }
        finally
        {
            IsBusy.Value = false;
        }
    }

    /// <summary>Mark all notifications as read.</summary>
    [RelayCommand]
    private async Task MarkAllAsReadAsync()
    {
        if (IsBusy.Value)
            return;

        IsBusy.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await api.MarkAllRead().FirstAsync(cts.Token);

            // Clear all unread notifications locally.
            var read = Notifications.Where(n => n.Unread).ToList();
            foreach (var n in read)
                Notifications.Remove(n);
            UnreadCount.Value = 0;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Mark all as read failed: {ex.Message}";
        }
        finally
        {
            IsBusy.Value = false;
        }
    }

    /// <summary>Open a notification's subject in the browser.</summary>
    [RelayCommand]
    private async Task OpenInBrowserAsync(string url)
    {
        if (!string.IsNullOrEmpty(url))
            await _browserLauncher.OpenAsync(url);
    }

    public void Dispose()
    {
        _poller.NotificationsUpdated -= OnNotificationsUpdated;
        _poller.IsPollingChanged -= OnPollingChanged;
        UnreadCount.Dispose();
        IsPolling.Dispose();
        IsBusy.Dispose();
        ErrorMessage.Dispose();
        IsAuthenticated.Dispose();
    }
}
