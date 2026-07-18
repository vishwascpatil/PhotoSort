using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class MemoryItemViewModel : ObservableObject
{
    private readonly IThumbnailService _thumbnailService;

    public MemoryItemViewModel(Memory memory, IThumbnailService thumbnailService)
    {
        _thumbnailService = thumbnailService;
        MemoryId = memory.Id;
        Title = memory.Title;
        Subtitle = memory.Subtitle ?? string.Empty;
        Type = memory.Type;
        Score = memory.Score;
        IsFavorite = memory.Photos.Any(p => p.Role == "Cover");
        CoverPhotoId = memory.CoverPhotoId;
        Memory = memory;
    }

    public Guid MemoryId { get; }
    public MemoryType Type { get; }
    public Memory Memory { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private double _score;

    [ObservableProperty]
    private BitmapSource? _coverBitmap;

    public int CoverPhotoId { get; }

    public string TypeIcon => Type switch
    {
        MemoryType.Day => "\uD83D\uDCC5",
        MemoryType.Week => "\uD83D\uDCC5",
        MemoryType.Month => "\uD83D\uDCC6",
        MemoryType.Trip => "\u2708\uFE0F",
        MemoryType.Person => "\uD83D\uDC64",
        MemoryType.Location => "\uD83D\uDDFA\uFE0F",
        MemoryType.Holiday => "\uD83C\uDF84",
        MemoryType.Season => "\uD83C\uDF42",
        MemoryType.Activity => "\u26BD",
        MemoryType.Video => "\uD83C\uDFA5",
        _ => "\uD83D\uDCF7"
    };

    public async Task LoadCoverAsync()
    {
        try
        {
            var thumbPath = _thumbnailService.GetThumbnailPath(CoverPhotoId, ThumbnailSize.Medium);
            if (!string.IsNullOrEmpty(thumbPath) && System.IO.File.Exists(thumbPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(thumbPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                CoverBitmap = bitmap;
            }
        }
        catch
        {
            // Silently handle load failures
        }
    }
}
