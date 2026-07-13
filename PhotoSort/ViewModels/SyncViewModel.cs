using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class SyncViewModel : ObservableObject, IDisposable
{
    private readonly ILibrarySynchronizationService _syncService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<SyncViewModel> _logger;

    private bool _disposed;

    [ObservableProperty]
    private SyncProgress _currentProgress = new()
    {
        Phase = SyncPhase.Idle
    };

    [ObservableProperty]
    private bool _isSynchronizing;

    [ObservableProperty]
    private bool _isWatching;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _statusMessage = "Ready to synchronize";

    [ObservableProperty]
    private string _lastSyncDisplay = "Never";

    [ObservableProperty]
    private int _filesAddedCount;

    [ObservableProperty]
    private int _filesDeletedCount;

    [ObservableProperty]
    private int _filesModifiedCount;

    [ObservableProperty]
    private int _filesRenamedCount;

    public ObservableCollection<string> WatchedFolders { get; } = [];
    public ObservableCollection<string> RecentEvents { get; } = [];

    public SyncViewModel(
        ILibrarySynchronizationService syncService,
        INavigationService navigationService,
        ILogger<SyncViewModel> logger)
    {
        _syncService = syncService;
        _navigationService = navigationService;
        _logger = logger;

        _syncService.ProgressChanged += OnProgressChanged;
        _syncService.SynchronizationCompleted += OnSynchronizationCompleted;
    }

    [RelayCommand]
    private async Task StartWatchingAsync()
    {
        if (IsWatching)
            return;

        try
        {
            await _syncService.StartWatchingAsync();
            IsWatching = true;
            StatusMessage = "Watching for file changes...";
            RefreshWatchedFolders();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start watching");
            StatusMessage = "Failed to start watching";
        }
    }

    [RelayCommand]
    private async Task StopWatchingAsync()
    {
        if (!IsWatching)
            return;

        try
        {
            await _syncService.StopWatchingAsync();
            IsWatching = false;
            StatusMessage = "Stopped watching";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop watching");
            StatusMessage = "Failed to stop watching";
        }
    }

    [RelayCommand]
    private async Task SyncAllAsync()
    {
        if (IsSynchronizing)
            return;

        IsSynchronizing = true;
        IsPaused = false;
        StatusMessage = "Synchronizing all folders...";
        FilesAddedCount = 0;
        FilesDeletedCount = 0;
        FilesModifiedCount = 0;
        FilesRenamedCount = 0;

        try
        {
            await _syncService.SynchronizeAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Synchronization failed");
            StatusMessage = "Synchronization failed";
        }
        finally
        {
            IsSynchronizing = false;
            IsPaused = false;
        }
    }

    [RelayCommand]
    private void PauseSync()
    {
        if (!IsSynchronizing)
            return;

        _syncService.PauseSync();
        IsPaused = true;
        StatusMessage = "Synchronization paused";
    }

    [RelayCommand]
    private void ResumeSync()
    {
        _syncService.ResumeSync();
        IsPaused = false;
        StatusMessage = "Resuming synchronization...";
    }

    [RelayCommand]
    private void CancelSync()
    {
        _syncService.CancelSync();
        IsSynchronizing = false;
        IsPaused = false;
        StatusMessage = "Synchronization cancelled";
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateTo<GalleryViewModel>();
    }

    private void OnProgressChanged(object? sender, SyncProgress progress)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentProgress = progress;
            FilesAddedCount = progress.FilesAdded;
            FilesDeletedCount = progress.FilesDeleted;
            FilesModifiedCount = progress.FilesModified;
            FilesRenamedCount = progress.FilesRenamed;

            if (progress.Phase != SyncPhase.Paused)
            {
                StatusMessage = progress.PhaseDisplay;
            }

            if (progress.LastSyncTime != default)
            {
                LastSyncDisplay = progress.LastSyncTime.ToString("MMM dd, yyyy HH:mm");
            }
        });
    }

    private void OnSynchronizationCompleted(object? sender, SyncCompletedEventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"Synchronized: +{e.FilesAdded} -{e.FilesDeleted} ~{e.FilesModified} ={e.FilesRenamed}";

            var eventMsg = $"[{DateTime.Now:HH:mm:ss}] Sync completed: +{e.FilesAdded} -{e.FilesDeleted} ~{e.FilesModified} ={e.FilesRenamed}";
            RecentEvents.Insert(0, eventMsg);

            if (RecentEvents.Count > 50)
            {
                RecentEvents.RemoveAt(RecentEvents.Count - 1);
            }
        });
    }

    private void RefreshWatchedFolders()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            WatchedFolders.Clear();
            var progress = _syncService.GetProgress();
            if (progress.ActiveWatcherCount > 0)
            {
                WatchedFolders.Add($"{progress.ActiveWatcherCount} active watcher(s)");
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _syncService.ProgressChanged -= OnProgressChanged;
        _syncService.SynchronizationCompleted -= OnSynchronizationCompleted;
        WatchedFolders.Clear();
        RecentEvents.Clear();
    }
}
