using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class SimilarPhotosViewModel : ObservableObject, IDisposable
{
    private readonly ISimilarPhotoService _similarPhotoService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<SimilarPhotosViewModel> _logger;

    private bool _disposed;

    [ObservableProperty]
    private SimilarPhotoDetectionProgress _currentProgress = new()
    {
        Phase = SimilarDetectionPhase.Idle
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
    private SimilarPhotoGroup? _selectedGroup;

    [ObservableProperty]
    private SimilarPhotoItem? _selectedPhoto;

    [ObservableProperty]
    private string _statusMessage = "Ready to scan for similar photos";

    public ObservableCollection<SimilarPhotoGroup> SimilarGroups { get; } = [];
    public ObservableCollection<SimilarPhotoItem> SelectedGroupPhotos { get; } = [];

    public SimilarPhotosViewModel(
        ISimilarPhotoService similarPhotoService,
        INavigationService navigationService,
        ILogger<SimilarPhotosViewModel> logger)
    {
        _similarPhotoService = similarPhotoService;
        _navigationService = navigationService;
        _logger = logger;

        _similarPhotoService.ProgressChanged += OnProgressChanged;
        _similarPhotoService.DetectionCompleted += OnDetectionCompleted;
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (_similarPhotoService.IsRunning)
            return;

        SimilarGroups.Clear();
        SelectedGroupPhotos.Clear();
        HasResults = false;
        SelectedGroup = null;
        SelectedGroupIndex = -1;
        IsRunning = true;
        IsPaused = false;
        StatusMessage = "Scanning for similar photos...";

        try
        {
            await _similarPhotoService.StartDetectionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start similar photo detection");
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
        if (!_similarPhotoService.IsRunning)
            return;

        _similarPhotoService.PauseDetection();
        IsPaused = true;
        StatusMessage = "Scan paused";
    }

    [RelayCommand]
    private void ResumeScan()
    {
        _similarPhotoService.ResumeDetection();
        IsPaused = false;
        StatusMessage = "Resuming scan...";
    }

    [RelayCommand]
    private void CancelScan()
    {
        _similarPhotoService.CancelDetection();
        IsRunning = false;
        IsPaused = false;
        StatusMessage = "Scan cancelled";
    }

    [RelayCommand]
    private async Task DeletePhotoAsync(SimilarPhotoItem? photo)
    {
        if (photo is null)
            return;

        try
        {
            await _similarPhotoService.DeletePhotoAsync(photo.Id);

            if (SelectedGroup is not null)
            {
                SelectedGroup.SimilarPhotos.Remove(photo);

                if (SelectedGroup.SimilarPhotos.Count == 0)
                {
                    SimilarGroups.Remove(SelectedGroup);
                    SelectedGroup = SimilarGroups.FirstOrDefault();
                }
            }

            SelectedGroupPhotos.Remove(photo);

            StatusMessage = $"Deleted: {photo.FileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete photo");
            StatusMessage = "Failed to delete photo";
        }
    }

    [RelayCommand]
    private async Task KeepBestAsync()
    {
        if (SelectedGroup is null)
            return;

        var photosToDelete = SelectedGroup.SimilarPhotos.Where(p => !p.IsBest).ToList();

        foreach (var photo in photosToDelete)
        {
            try
            {
                await _similarPhotoService.DeletePhotoAsync(photo.Id);
                SelectedGroup.SimilarPhotos.Remove(photo);
                SelectedGroupPhotos.Remove(photo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete photo {PhotoId}", photo.Id);
            }
        }

        if (SelectedGroup.SimilarPhotos.Count == 0)
        {
            SimilarGroups.Remove(SelectedGroup);
            SelectedGroup = SimilarGroups.FirstOrDefault();
        }

        StatusMessage = $"Kept best photo and deleted {photosToDelete.Count} similar photos";
    }

    [RelayCommand]
    private async Task IgnoreGroupAsync()
    {
        if (SelectedGroup is null)
            return;

        try
        {
            await _similarPhotoService.IgnoreGroupAsync(SelectedGroup.GroupId);

            SimilarGroups.Remove(SelectedGroup);
            SelectedGroup = SimilarGroups.FirstOrDefault();

            StatusMessage = "Group ignored";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ignore group");
            StatusMessage = "Failed to ignore group";
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateTo<GalleryViewModel>();
    }

    partial void OnSelectedGroupIndexChanged(int value)
    {
        if (value >= 0 && value < SimilarGroups.Count)
        {
            SelectedGroup = SimilarGroups[value];

            SelectedGroupPhotos.Clear();
            if (SelectedGroup?.BestPhoto is not null)
            {
                SelectedGroupPhotos.Add(new SimilarPhotoItem
                {
                    Id = SelectedGroup.BestPhoto.Id,
                    FilePath = SelectedGroup.BestPhoto.FilePath,
                    FileName = SelectedGroup.BestPhoto.FileName,
                    FileSize = SelectedGroup.BestPhoto.FileSize,
                    DateTaken = SelectedGroup.BestPhoto.DateTaken,
                    ThumbnailSmallPath = SelectedGroup.BestPhoto.ThumbnailSmallPath,
                    PerceptualHash = SelectedGroup.ReferenceHash,
                    HammingDistance = 0,
                    IsBest = true,
                    Width = SelectedGroup.BestPhoto.Width,
                    Height = SelectedGroup.BestPhoto.Height
                });
            }

            foreach (var photo in SelectedGroup?.SimilarPhotos ?? [])
            {
                SelectedGroupPhotos.Add(photo);
            }
        }
        else
        {
            SelectedGroup = null;
            SelectedGroupPhotos.Clear();
        }
    }

    private void OnProgressChanged(object? sender, SimilarPhotoDetectionProgress progress)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentProgress = progress;

            if (progress.Phase == SimilarDetectionPhase.Completed)
            {
                StatusMessage = $"Found {progress.GroupsFound} groups with similar photos";
            }
            else if (progress.Phase != SimilarDetectionPhase.Paused)
            {
                StatusMessage = progress.PhaseDisplay;
            }
        });
    }

    private void OnDetectionCompleted(object? sender, IReadOnlyList<SimilarPhotoGroup> groups)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            SimilarGroups.Clear();
            foreach (var group in groups)
            {
                SimilarGroups.Add(group);
            }

            HasResults = SimilarGroups.Count > 0;

            if (HasResults)
            {
                SelectedGroupIndex = 0;
                StatusMessage = $"Found {SimilarGroups.Count} groups. Potential savings: {CurrentProgress.DisplayReclaimable}";
            }
            else
            {
                StatusMessage = "No similar photos found";
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _similarPhotoService.ProgressChanged -= OnProgressChanged;
        _similarPhotoService.DetectionCompleted -= OnDetectionCompleted;
        SimilarGroups.Clear();
        SelectedGroupPhotos.Clear();
    }
}
