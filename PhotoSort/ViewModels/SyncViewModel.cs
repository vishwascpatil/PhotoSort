using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class SyncViewModel : ObservableObject, IDisposable
{
    private readonly ILibrarySynchronizationService _syncService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<SyncViewModel> _logger;
    private readonly IPhotoRepository _photoRepository;
    private readonly IFolderRepository _folderRepository;

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

    [ObservableProperty]
    private int _totalPhotoCount;

    [ObservableProperty]
    private string _elapsedDisplay = "--";

    public ObservableCollection<string> WatchedFolders { get; } = [];
    public ObservableCollection<string> RecentEvents { get; } = [];

    public SyncViewModel(
        ILibrarySynchronizationService syncService,
        INavigationService navigationService,
        ILogger<SyncViewModel> logger,
        IPhotoRepository photoRepository,
        IFolderRepository folderRepository)
    {
        _syncService = syncService;
        _navigationService = navigationService;
        _logger = logger;
        _photoRepository = photoRepository;
        _folderRepository = folderRepository;

        _syncService.ProgressChanged += OnProgressChanged;
        _syncService.SynchronizationCompleted += OnSynchronizationCompleted;

        _ = LoadLibraryStatsAsync();
    }

    private async Task LoadLibraryStatsAsync()
    {
        try
        {
            var photos = await _photoRepository.GetAllAsync();
            TotalPhotoCount = photos.Count;

            var folders = await _folderRepository.GetAllAsync();
            WatchedFolders.Clear();
            foreach (var folder in folders)
            {
                WatchedFolders.Add(folder.FolderPath);
            }

            if (folders.Count == 0)
            {
                WatchedFolders.Add("No folders added yet. Add folders from the Gallery view.");
            }
        }
        catch { }
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
            await RefreshWatchedFoldersAsync();
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
        StatusMessage = "Scanning library for changes...";
        FilesAddedCount = 0;
        FilesDeletedCount = 0;
        FilesModifiedCount = 0;
        FilesRenamedCount = 0;
        ElapsedDisplay = "0s";

        var startTime = DateTime.UtcNow;

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

            var elapsed = DateTime.UtcNow - startTime;
            ElapsedDisplay = elapsed.TotalSeconds < 60
                ? $"{(int)elapsed.TotalSeconds}s"
                : $"{elapsed.TotalMinutes:F1}m";

            await LoadLibraryStatsAsync();
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
        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            StatusMessage = $"Synchronized: +{e.FilesAdded} -{e.FilesDeleted} ~{e.FilesModified} ={e.FilesRenamed}";

            var parts = new List<string>();
            if (e.FilesAdded > 0) parts.Add($"+{e.FilesAdded} added");
            if (e.FilesDeleted > 0) parts.Add($"-{e.FilesDeleted} removed");
            if (e.FilesModified > 0) parts.Add($"~{e.FilesModified} modified");
            if (e.FilesRenamed > 0) parts.Add($"={e.FilesRenamed} renamed");
            var summary = parts.Count > 0 ? string.Join(", ", parts) : "No changes found";

            var eventMsg = $"[{DateTime.Now:HH:mm}] Scan complete in {e.Duration.TotalSeconds:F0}s — {summary}";
            RecentEvents.Insert(0, eventMsg);

            if (RecentEvents.Count > 50)
            {
                RecentEvents.RemoveAt(RecentEvents.Count - 1);
            }

            if (_navigationService.CurrentViewModel is GalleryViewModel galleryVm)
            {
                await galleryVm.RefreshCommand.ExecuteAsync(null);
            }
        });
    }

    private async Task RefreshWatchedFoldersAsync()
    {
        await Application.Current?.Dispatcher.InvokeAsync(() =>
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
