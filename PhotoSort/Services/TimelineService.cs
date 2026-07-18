using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class TimelineService : ITimelineService
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly ILogger<TimelineService> _logger;

    public TimelineService(
        IDbContextFactory<PhotoSortDbContext> contextFactory,
        ILogger<TimelineService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TimelineYearGroup>> GetYearGroupsAsync()
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var yearGroups = await context.Photos
                .AsNoTracking()
                .GroupBy(p => (p.DateTaken ?? p.ModifiedDateUtc).Year)
                .Select(g => new TimelineYearGroup
                {
                    Year = g.Key,
                    PhotoCount = g.Count()
                })
                .OrderByDescending(y => y.Year)
                .ToListAsync();

            sw.Stop();
            _logger.LogDebug("GetYearGroupsAsync: {Count} years in {Elapsed}ms",
                yearGroups.Count, sw.ElapsedMilliseconds);

            return yearGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get year groups");
            return [];
        }
    }

    public async Task<IReadOnlyList<TimelineMonthGroup>> GetMonthGroupsAsync(int year)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var monthGroups = await context.Photos
                .AsNoTracking()
                .Where(p => (p.DateTaken ?? p.ModifiedDateUtc).Year == year)
                .GroupBy(p => (p.DateTaken ?? p.ModifiedDateUtc).Month)
                .Select(g => new TimelineMonthGroup
                {
                    Year = year,
                    Month = g.Key,
                    PhotoCount = g.Count()
                })
                .OrderByDescending(m => m.Month)
                .ToListAsync();

            sw.Stop();
            _logger.LogDebug("GetMonthGroupsAsync({Year}): {Count} months in {Elapsed}ms",
                year, monthGroups.Count, sw.ElapsedMilliseconds);

            return monthGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get month groups for year {Year}", year);
            return [];
        }
    }

    public async Task<IReadOnlyList<TimelineDayGroup>> GetDayGroupsAsync(int year, int month)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var dayGroups = await context.Photos
                .AsNoTracking()
                .Where(p => (p.DateTaken ?? p.ModifiedDateUtc).Year == year
                    && (p.DateTaken ?? p.ModifiedDateUtc).Month == month)
                .GroupBy(p => (p.DateTaken ?? p.ModifiedDateUtc).Day)
                .Select(g => new TimelineDayGroup
                {
                    Year = year,
                    Month = month,
                    Day = g.Key,
                    Date = new DateTime(year, month, g.Key),
                    PhotoCount = g.Count()
                })
                .OrderByDescending(d => d.Day)
                .ToListAsync();

            sw.Stop();
            _logger.LogDebug("GetDayGroupsAsync({Year},{Month}): {Count} days in {Elapsed}ms",
                year, month, dayGroups.Count, sw.ElapsedMilliseconds);

            return dayGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get day groups for {Year}-{Month}", year, month);
            return [];
        }
    }

    public async Task<IReadOnlyList<GalleryPhoto>> GetPhotosForDayAsync(int year, int month, int day)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var photos = await context.Photos
                .AsNoTracking()
                .Where(p => (p.DateTaken ?? p.ModifiedDateUtc).Year == year
                    && (p.DateTaken ?? p.ModifiedDateUtc).Month == month
                    && (p.DateTaken ?? p.ModifiedDateUtc).Day == day)
                .OrderByDescending(p => p.DateTaken ?? p.ModifiedDateUtc)
                .Select(p => new GalleryPhoto
                {
                    Id = p.Id,
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
                    VideoThumbnailLargePath = p.VideoThumbnailLargePath,
                    IsFavorite = p.IsFavorite,
                    ModifiedDateUtc = p.ModifiedDateUtc,
                    FolderId = p.FolderId,
                    State = p.State,
                    DateTakenYear = p.DateTaken != null ? p.DateTaken.Value.Year : p.ModifiedDateUtc.Year,
                    DateTakenMonth = p.DateTaken != null ? p.DateTaken.Value.Month : p.ModifiedDateUtc.Month,
                    DateTakenDay = p.DateTaken != null ? p.DateTaken.Value.Day : p.ModifiedDateUtc.Day
                })
                .ToListAsync();

            sw.Stop();
            _logger.LogDebug("GetPhotosForDayAsync({0}-{1}-{2}): {Count} photos in {Elapsed}ms",
                year, month, day, photos.Count, sw.ElapsedMilliseconds);

            return photos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get photos for {Year}-{Month}-{Day}", year, month, day);
            return [];
        }
    }

    public async Task<TimelineStats> GetTimelineStatsAsync()
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var stats = await context.Photos
                .AsNoTracking()
                .GroupBy(p => 1)
                .Select(g => new TimelineStats
                {
                    TotalPhotos = g.Count(),
                    YearsCount = g.Select(p => (p.DateTaken ?? p.ModifiedDateUtc).Year).Distinct().Count(),
                    MonthsCount = g.Select(p => new { Year = (p.DateTaken ?? p.ModifiedDateUtc).Year, Month = (p.DateTaken ?? p.ModifiedDateUtc).Month })
                                   .Distinct().Count(),
                    DaysCount = g.Select(p => new { Year = (p.DateTaken ?? p.ModifiedDateUtc).Year, Month = (p.DateTaken ?? p.ModifiedDateUtc).Month, Day = (p.DateTaken ?? p.ModifiedDateUtc).Day })
                                 .Distinct().Count(),
                    EarliestYear = g.Min(p => (p.DateTaken ?? p.ModifiedDateUtc).Year),
                    LatestYear = g.Max(p => (p.DateTaken ?? p.ModifiedDateUtc).Year)
                })
                .FirstOrDefaultAsync() ?? new TimelineStats();

            sw.Stop();

            return new TimelineStats
            {
                TotalPhotos = stats.TotalPhotos,
                YearsCount = stats.YearsCount,
                MonthsCount = stats.MonthsCount,
                DaysCount = stats.DaysCount,
                EarliestYear = stats.EarliestYear,
                LatestYear = stats.LatestYear,
                QueryLatencyMs = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get timeline stats");
            return new TimelineStats();
        }
    }
}
