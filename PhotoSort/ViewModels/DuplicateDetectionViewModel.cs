using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class DuplicateDetectionViewModel : ObservableObject, IDisposable
{
    private readonly IDuplicateDetectionService _duplicateDetectionService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<DuplicateDetectionViewModel> _logger;

    private bool _disposed;

    [ObservableProperty]
    private DuplicateDetectionProgress _currentProgress = new()
    {
        Phase = DuplicateDetectionPhase.Idle
    };

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private int _selectedGroupIndex = -1;

    [ObservableProperty]
    private DuplicateGroup? _selectedGroup;

    [ObservableProperty]
    private string _statusMessage = "Ready to scan for duplicates";

    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = [];

    public DuplicateDetectionViewModel(
        IDuplicateDetectionService duplicateDetectionService,
        INavigationService navigationService,
        ILogger<DuplicateDetectionViewModel> logger)
    {
        _duplicateDetectionService = duplicateDetectionService;
        _navigationService = navigationService;
        _logger = logger;

        _duplicateDetectionService.ProgressChanged += OnProgressChanged;
        _duplicateDetectionService.DetectionCompleted += OnDetectionCompleted;
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (_duplicateDetectionService.IsRunning)
            return;

        DuplicateGroups.Clear();
        HasResults = false;
        SelectedGroup = null;
        SelectedGroupIndex = -1;
        IsRunning = true;
        IsPaused = false;
        StatusMessage = "Scanning for duplicates...";

        try
        {
            await _duplicateDetectionService.StartDetectionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start duplicate detection");
            StatusMessage = "Scan failed. Please try again.";
        }
        finally
        {
            IsRunning = false;
            IsPaused = false;
        }
    }

    [RelayCommand]
    private void PauseScan()
    {
        if (!_duplicateDetectionService.IsRunning)
            return;

        _duplicateDetectionService.PauseDetection();
        IsPaused = true;
        StatusMessage = "Scan paused";
    }

    [RelayCommand]
    private void ResumeScan()
    {
        _duplicateDetectionService.ResumeDetection();
        IsPaused = false;
        StatusMessage = "Resuming scan...";
    }

    [RelayCommand]
    private void CancelScan()
    {
        _duplicateDetectionService.CancelDetection();
        IsRunning = false;
        IsPaused = false;
        StatusMessage = "Scan cancelled";
    }

    [RelayCommand]
    private async Task DeleteDuplicateAsync(DuplicatePhoto? duplicate)
    {
        if (duplicate is null)
            return;

        try
        {
            await _duplicateDetectionService.DeleteDuplicateAsync(duplicate.Id);

            if (SelectedGroup is not null)
            {
                SelectedGroup.Duplicates.Remove(duplicate);

                if (SelectedGroup.Duplicates.Count == 0)
                {
                    DuplicateGroups.Remove(SelectedGroup);
                    SelectedGroup = DuplicateGroups.FirstOrDefault();
                }
            }

            StatusMessage = $"Deleted: {duplicate.FileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete duplicate");
            StatusMessage = "Failed to delete duplicate";
        }
    }

    [RelayCommand]
    private async Task MarkAsOriginalAsync(DuplicatePhoto? duplicate)
    {
        if (duplicate is null || SelectedGroup is null)
            return;

        try
        {
            await _duplicateDetectionService.MarkAsOriginalAsync(
                SelectedGroup.GroupId, duplicate.Id);

            // Update UI
            duplicate.IsOriginal = true;
            if (SelectedGroup.OriginalPhoto is not null)
            {
                var oldOriginal = new DuplicatePhoto
                {
                    Id = SelectedGroup.OriginalPhoto.Id,
                    FilePath = SelectedGroup.OriginalPhoto.FilePath,
                    FileName = SelectedGroup.OriginalPhoto.FileName,
                    FileSize = SelectedGroup.OriginalPhoto.FileSize,
                    DateTaken = SelectedGroup.OriginalPhoto.DateTaken,
                    ThumbnailSmallPath = SelectedGroup.OriginalPhoto.ThumbnailSmallPath,
                    IsOriginal = false
                };

                SelectedGroup.Duplicates.Remove(duplicate);
                SelectedGroup.Duplicates.Add(oldOriginal);

                SelectedGroup.OriginalPhoto = new GalleryPhoto
                {
                    Id = duplicate.Id,
                    FilePath = duplicate.FilePath,
                    FileName = duplicate.FileName,
                    Extension = System.IO.Path.GetExtension(duplicate.FilePath),
                    FileSize = duplicate.FileSize,
                    DateTaken = duplicate.DateTaken,
                    ThumbnailSmallPath = duplicate.ThumbnailSmallPath,
                    ModifiedDateUtc = DateTime.UtcNow,
                    FolderId = 0
                };
            }

            StatusMessage = $"Marked {duplicate.FileName} as original";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark as original");
            StatusMessage = "Failed to mark as original";
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateTo<GalleryViewModel>();
    }

    partial void OnSelectedGroupIndexChanged(int value)
    {
        if (value >= 0 && value < DuplicateGroups.Count)
        {
            SelectedGroup = DuplicateGroups[value];
        }
        else
        {
            SelectedGroup = null;
        }
    }

    private void OnProgressChanged(object? sender, DuplicateDetectionProgress progress)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentProgress = progress;

            if (progress.Phase == DuplicateDetectionPhase.Completed)
            {
                StatusMessage = $"Found {progress.DuplicateGroupsFound} duplicate groups with {progress.DuplicatesFound} files";
            }
            else if (progress.Phase != DuplicateDetectionPhase.Paused)
            {
                StatusMessage = progress.PhaseDisplay;
            }
        });
    }

    private void OnDetectionCompleted(object? sender, IReadOnlyList<DuplicateGroup> groups)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            DuplicateGroups.Clear();
            foreach (var group in groups)
            {
                DuplicateGroups.Add(group);
            }

            HasResults = DuplicateGroups.Count > 0;

            if (HasResults)
            {
                SelectedGroupIndex = 0;
                StatusMessage = $"Found {DuplicateGroups.Count} duplicate groups. " +
                              $"Potential savings: {CurrentProgress.DisplayReclaimable}";
            }
            else
            {
                StatusMessage = "No duplicates found";
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _duplicateDetectionService.ProgressChanged -= OnProgressChanged;
        _duplicateDetectionService.DetectionCompleted -= OnDetectionCompleted;
        DuplicateGroups.Clear();
    }
}
