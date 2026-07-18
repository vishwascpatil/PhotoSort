using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class QualitySignalExtractor : ISignalExtractor
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<QualitySignalExtractor> _logger;
    private static readonly HashSet<string> ImageExts = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff", ".tif"];

    public QualitySignalExtractor(IPhotoRepository photoRepo, ILogger<QualitySignalExtractor> logger)
    {
        _photoRepo = photoRepo;
        _logger = logger;
    }

    public bool CanExtract(string extension) => ImageExts.Contains(extension.ToLowerInvariant());

    public async Task ExtractAsync(int photoId, CancellationToken ct = default)
    {
        try
        {
            var photo = await _photoRepo.GetByIdAsync(photoId);
            if (photo is null)
                return;

            var quality = photo.Width > 2000 && photo.Height > 1500 ? 0.9
                : photo.Width > 1000 ? 0.7
                : photo.Width > 500 ? 0.5
                : 0.3;

            var signal = new QualitySignal
            {
                Sharpness = quality,
                Exposure = 0.8,
                Contrast = 0.7,
                Composition = 0.6,
                Overall = quality * 0.35 + 0.8 * 0.25 + 0.7 * 0.2 + 0.6 * 0.15 + 0.05
            };
            var photoSignal = new PhotoSignal { PhotoId = photoId };
            photoSignal.SetTyped("quality", signal);
            _logger.LogDebug("Extracted quality signal for photo {PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract quality signal for photo {PhotoId}", photoId);
        }
    }
}
