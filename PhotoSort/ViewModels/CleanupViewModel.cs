using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class CleanupViewModel : ObservableObject, IDisposable
{
    private readonly IMediaClassificationService _classificationService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<CleanupViewModel> _logger;

    private bool _disposed;
    private bool _isClassifying;

    [ObservableProperty]
    private bool _isClassifyingInProgress;

    [ObservableProperty]
    private CleanupStatistics _statistics = new();

    [ObservableProperty]
    private CleanupCategory? _selectedCategory;

    [ObservableProperty]
    private string _statusMessage = "Ready to classify media";

    [ObservableProperty]
    private int _classificationProgress;

    [ObservableProperty]
    private bool _hasCategories;

    [ObservableProperty]
    private bool _hasSelectedPhotos;

    [ObservableProperty]
    private int _totalIssuesCount;

    [ObservableProperty]
    private int _duplicatesCount;

    [ObservableProperty]
    private int _clutterCount;

    [ObservableProperty]
    private int _qualityCount;

    public ObservableCollection<CleanupCategory> Categories { get; } = [];
    public ObservableCollection<GalleryPhoto> SelectedCategoryPhotos { get; } = [];
    public ObservableCollection<int> SelectedPhotoIds { get; } = [];

    public CleanupViewModel(
        IMediaClassificationService classificationService,
        INavigationService navigationService,
        ILogger<CleanupViewModel> logger)
    {
        _classificationService = classificationService;
        _navigationService = navigationService;
        _logger = logger;

        _classificationService.ProgressChanged += OnProgressChanged;
    }

    [RelayCommand]
    private async Task ClassifyAllAsync()
    {
        if (_isClassifying)
            return;

        _isClassifying = true;
        IsClassifyingInProgress = true;
        StatusMessage = "Classifying media files...";
        ClassificationProgress = 0;

        try
        {
            var count = await _classificationService.ClassifyAllAsync();
            StatusMessage = $"Classified {count} files";

            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Classification failed");
            StatusMessage = "Classification failed";
        }
        finally
        {
            _isClassifying = false;
            IsClassifyingInProgress = false;
        }
    }

    [RelayCommand]
    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _classificationService.GetCleanupCategoriesAsync();
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            HasCategories = Categories.Count > 0;

            Statistics = await _classificationService.GetStatisticsAsync();

            DuplicatesCount = Categories
                .Where(c => c.Category == MediaCategory.WhatsAppImage || c.Category == MediaCategory.WhatsAppVideo
                    || c.Category == MediaCategory.TelegramImage || c.Category == MediaCategory.TelegramVideo)
                .Sum(c => c.FileCount);

            ClutterCount = Categories
                .Where(c => c.Category == MediaCategory.Meme || c.Category == MediaCategory.Screenshot
                    || c.Category == MediaCategory.ScreenRecording)
                .Sum(c => c.FileCount);

            QualityCount = Categories
                .Where(c => c.Category == MediaCategory.DownloadedImage || c.Category == MediaCategory.DownloadedVideo
                    || c.Category == MediaCategory.SocialMediaImage || c.Category == MediaCategory.SocialMediaVideo)
                .Sum(c => c.FileCount);

            TotalIssuesCount = DuplicatesCount + ClutterCount + QualityCount;

            StatusMessage = HasCategories
                ? $"Found {Categories.Count} categories with {Statistics.ClassifiedFiles} classified files"
                : "No classified media found. Run classification first.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load categories");
            StatusMessage = "Failed to load categories";
        }
    }

    [RelayCommand]
    private async Task SelectCategoryAsync(CleanupCategory? category)
    {
        if (category is null)
            return;

        SelectedCategory = category;
        SelectedPhotoIds.Clear();
        HasSelectedPhotos = false;

        StatusMessage = $"Loading {category.DisplayName}...";

        try
        {
            var photos = await _classificationService.GetPhotosByCategoryAsync(category.Category);
            SelectedCategoryPhotos.Clear();
            foreach (var photo in photos)
            {
                SelectedCategoryPhotos.Add(photo);
            }

            StatusMessage = $"Showing {SelectedCategoryPhotos.Count} files in {category.DisplayName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load photos for category");
            StatusMessage = "Failed to load photos";
        }
    }

    [RelayCommand]
    private void TogglePhotoSelection(GalleryPhoto? photo)
    {
        if (photo is null)
            return;

        if (SelectedPhotoIds.Contains(photo.Id))
        {
            SelectedPhotoIds.Remove(photo.Id);
        }
        else
        {
            SelectedPhotoIds.Add(photo.Id);
        }

        HasSelectedPhotos = SelectedPhotoIds.Count > 0;
    }

    [RelayCommand]
    private void SelectAllPhotos()
    {
        SelectedPhotoIds.Clear();
        foreach (var photo in SelectedCategoryPhotos)
        {
            SelectedPhotoIds.Add(photo.Id);
        }

        HasSelectedPhotos = SelectedPhotoIds.Count > 0;
    }

    [RelayCommand]
    private void DeselectAllPhotos()
    {
        SelectedPhotoIds.Clear();
        HasSelectedPhotos = false;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedCategory is null || SelectedPhotoIds.Count == 0)
            return;

        var count = SelectedPhotoIds.Count;
        var size = SelectedCategoryPhotos
            .Where(p => SelectedPhotoIds.Contains(p.Id))
            .Sum(p => p.FileSize);

        StatusMessage = $"Deleting {count} files ({FormatFileSize(size)})...";

        try
        {
            await _classificationService.DeletePhotosByCategoryAsync(
                SelectedCategory.Category,
                SelectedPhotoIds.ToList());

            // Remove deleted from view
            var toRemove = SelectedCategoryPhotos
                .Where(p => SelectedPhotoIds.Contains(p.Id))
                .ToList();

            foreach (var photo in toRemove)
            {
                SelectedCategoryPhotos.Remove(photo);
            }

            SelectedPhotoIds.Clear();
            HasSelectedPhotos = false;

            // Refresh categories
            await LoadCategoriesAsync();

            StatusMessage = $"Deleted {count} files, saved {FormatFileSize(size)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete photos");
            StatusMessage = "Failed to delete files";
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateTo<GalleryViewModel>();
    }

    private void OnProgressChanged(object? sender, int processed)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ClassificationProgress = processed;
            StatusMessage = $"Classified {processed} files...";
        });
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _classificationService.ProgressChanged -= OnProgressChanged;
        Categories.Clear();
        SelectedCategoryPhotos.Clear();
        SelectedPhotoIds.Clear();
    }
}
