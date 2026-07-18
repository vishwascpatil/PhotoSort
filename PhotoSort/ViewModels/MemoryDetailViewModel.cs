using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class MemoryDetailViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IPhotoRepository _photoRepository;
    private readonly ILogger<MemoryDetailViewModel> _logger;
    private Memory? _memory;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _dateRange = string.Empty;

    [ObservableProperty]
    private string? _locationSummary;

    [ObservableProperty]
    private string? _peopleSummary;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<GalleryPhoto> PhotoItems { get; } = [];

    public MemoryDetailViewModel(
        INavigationService navigationService,
        IPhotoRepository photoRepository,
        ILogger<MemoryDetailViewModel> logger)
    {
        _navigationService = navigationService;
        _photoRepository = photoRepository;
        _logger = logger;
    }

    public void Initialize(Memory memory)
    {
        _memory = memory;
        Title = memory.Title;
        Subtitle = memory.Subtitle ?? string.Empty;
        DateRange = $"{memory.DateStart:MMM d, yyyy}";
        if (memory.DateStart.Date != memory.DateEnd.Date)
            DateRange += $" \u2013 {memory.DateEnd:MMM d, yyyy}";
        LocationSummary = memory.LocationSummary;
        PeopleSummary = memory.PeopleSummary;
    }

    [RelayCommand]
    private async Task OpenPhotoAsync(object? parameter)
    {
        if (parameter is not int photoId || _memory is null) return;

        var memoryPhotoIds = _memory.Photos
            .OrderBy(p => p.SortOrder)
            .Select(p => p.PhotoId)
            .ToList();

        var photoEntities = await Task.WhenAll(
            memoryPhotoIds.Select(id => _photoRepository.GetByIdAsync(id)));

        var photos = photoEntities
            .Where(p => p is not null)
            .Select(p => new GalleryPhoto
            {
                Id = p!.Id,
                FilePath = p.FilePath,
                FileName = p.FileName,
                Extension = p.Extension,
                DateTaken = p.DateTaken,
                Width = p.Width,
                Height = p.Height,
                FileSize = p.FileSize,
                ThumbnailPath = p.ThumbnailPath,
                ThumbnailSmallPath = p.ThumbnailSmallPath,
                ThumbnailMediumPath = p.ThumbnailMediumPath,
                VideoThumbnailSmallPath = p.VideoThumbnailSmallPath,
                VideoThumbnailMediumPath = p.VideoThumbnailMediumPath,
            })
            .ToList();

        var index = photos.FindIndex(p => p.Id == photoId);
        if (index < 0) return;

        var viewer = App.Services.GetRequiredService<PhotoViewerViewModel>();
        viewer.Initialize(photos, index);
        _navigationService.NavigateTo(viewer);
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateTo<MemoriesViewModel>();
    }
}
