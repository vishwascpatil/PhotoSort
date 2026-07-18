using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class TemporalSignalExtractor : ISignalExtractor
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<TemporalSignalExtractor> _logger;
    private static readonly HashSet<string> AllExtensions = [.. GalleryPhoto.ImageExtensions, .. GalleryPhoto.VideoExtensions];

    public TemporalSignalExtractor(IPhotoRepository photoRepo, ILogger<TemporalSignalExtractor> logger)
    {
        _photoRepo = photoRepo;
        _logger = logger;
    }

    public bool CanExtract(string extension) => AllExtensions.Contains(extension.ToLowerInvariant());

    public async Task ExtractAsync(int photoId, CancellationToken ct = default)
    {
        try
        {
            var photo = await _photoRepo.GetByIdAsync(photoId);
            if (photo?.DateTaken is null)
                return;

            var date = photo.DateTaken.Value;
            var now = DateTime.UtcNow;
            var yearDelta = now.Year - date.Year;

            var signal = new TemporalSignal
            {
                Year = date.Year,
                Month = date.Month,
                Day = date.Day,
                Hour = date.Hour,
                YearDelta = yearDelta,
                Season = GetSeason(date),
                Holiday = GetHoliday(date),
                IsAnniversary = date.Month == now.Month && date.Day == now.Day,
                Weight = ComputeTemporalWeight(date, now, yearDelta)
            };

            var photoSignal = new PhotoSignal { PhotoId = photoId };
            photoSignal.SetTyped("temporal", signal);


            _logger.LogDebug("Extracted temporal signal for photo {PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract temporal signal for photo {PhotoId}", photoId);
        }
    }

    private static string GetSeason(DateTime date)
    {
        return date.Month switch
        {
            3 or 4 or 5 => "Spring",
            6 or 7 or 8 => "Summer",
            9 or 10 or 11 => "Autumn",
            _ => "Winter"
        };
    }

    private static string? GetHoliday(DateTime date)
    {
        return (date.Month, date.Day) switch
        {
            (1, 1) => "New Year",
            (2, 14) => "Valentine's Day",
            (10, 31) => "Halloween",
            (12, 24) or (12, 25) or (12, 26) => "Christmas",
            _ => null
        };
    }

    private static double ComputeTemporalWeight(DateTime date, DateTime now, int yearDelta)
    {
        if (yearDelta <= 0) return 0;
        var dayOfYearDelta = Math.Abs(now.DayOfYear - date.DayOfYear);
        if (dayOfYearDelta <= 3)
        {
            var weight = 1.0 - (dayOfYearDelta / 3.0);
            weight *= Math.Exp(-0.15 * yearDelta);
            return weight;
        }
        if (yearDelta > 5 && dayOfYearDelta <= 7)
            return 0.3 * Math.Exp(-0.15 * yearDelta);
        return 0;
    }
}
