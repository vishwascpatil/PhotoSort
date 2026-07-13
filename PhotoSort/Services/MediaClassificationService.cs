using System.Diagnostics;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class MediaClassificationService : IMediaClassificationService
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly IPhotoRepository _photoRepository;
    private readonly ILogger<MediaClassificationService> _logger;

    private bool _disposed;

    public event EventHandler<int>? ProgressChanged;

    // Folder path signals: folder name substring -> (category, confidence)
    private static readonly Dictionary<string, (MediaCategory Category, double Confidence)> FolderSignals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["whatsapp images"] = (MediaCategory.WhatsAppImage, 0.95),
        ["whatsapp video"] = (MediaCategory.WhatsAppVideo, 0.95),
        ["whatsapp image"] = (MediaCategory.WhatsAppImage, 0.90),
        ["telegram images"] = (MediaCategory.TelegramImage, 0.95),
        ["telegram video"] = (MediaCategory.TelegramVideo, 0.95),
        ["telegram image"] = (MediaCategory.TelegramImage, 0.90),
        ["downloads"] = (MediaCategory.DownloadedImage, 0.70),
        ["download"] = (MediaCategory.DownloadedImage, 0.65),
        ["screenshots"] = (MediaCategory.Screenshot, 0.95),
        ["screenshot"] = (MediaCategory.Screenshot, 0.90),
        ["dcim"] = (MediaCategory.CameraPhoto, 0.80),
        ["camera"] = (MediaCategory.CameraPhoto, 0.75),
        ["instagram"] = (MediaCategory.SocialMediaImage, 0.85),
        ["instasave"] = (MediaCategory.SocialMediaImage, 0.90),
        ["facebook"] = (MediaCategory.SocialMediaImage, 0.80),
        ["twitter"] = (MediaCategory.SocialMediaImage, 0.80),
        ["snapchat"] = (MediaCategory.SocialMediaImage, 0.80),
        ["tiktok"] = (MediaCategory.SocialMediaVideo, 0.85),
        ["memes"] = (MediaCategory.Meme, 0.90),
        ["meme"] = (MediaCategory.Meme, 0.85),
        ["screen recordings"] = (MediaCategory.ScreenRecording, 0.95),
        ["screen recording"] = (MediaCategory.ScreenRecording, 0.90)
    };

    // Filename prefix signals: prefix -> (category, confidence)
    private static readonly Dictionary<string, (MediaCategory Category, double Confidence)> FilenamePrefixSignals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["img_"] = (MediaCategory.CameraPhoto, 0.70),
        ["dsc_"] = (MediaCategory.CameraPhoto, 0.80),
        ["dscn"] = (MediaCategory.CameraPhoto, 0.80),
        ["p10"] = (MediaCategory.CameraPhoto, 0.75),
        ["mvimg"] = (MediaCategory.CameraPhoto, 0.75),
        ["vid_"] = (MediaCategory.CameraVideo, 0.70),
        ["mp4"] = (MediaCategory.CameraVideo, 0.60),
        ["whatsapp image"] = (MediaCategory.WhatsAppImage, 0.90),
        ["whatsapp video"] = (MediaCategory.WhatsAppVideo, 0.90),
        ["wa "] = (MediaCategory.WhatsAppImage, 0.70),
        ["screenshot_"] = (MediaCategory.Screenshot, 0.90),
        ["screenshot "] = (MediaCategory.Screenshot, 0.85),
        ["screen shot"] = (MediaCategory.Screenshot, 0.85),
        ["capture"] = (MediaCategory.Screenshot, 0.70),
        ["telegram"] = (MediaCategory.TelegramImage, 0.80),
        ["instasave"] = (MediaCategory.SocialMediaImage, 0.90),
        ["fb_"] = (MediaCategory.SocialMediaImage, 0.75),
        ["ig_"] = (MediaCategory.SocialMediaImage, 0.80),
        ["snap"] = (MediaCategory.SocialMediaImage, 0.70),
        ["download"] = (MediaCategory.DownloadedImage, 0.60),
        ["meme"] = (MediaCategory.Meme, 0.80)
    };

    // Filename contains signals: substring -> (category, confidence)
    private static readonly Dictionary<string, (MediaCategory Category, double Confidence)> FilenameContainsSignals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["whatsapp"] = (MediaCategory.WhatsAppImage, 0.80),
        ["telegram"] = (MediaCategory.TelegramImage, 0.75),
        ["screenshot"] = (MediaCategory.Screenshot, 0.85),
        ["screenrecording"] = (MediaCategory.ScreenRecording, 0.90),
        ["instasave"] = (MediaCategory.SocialMediaImage, 0.90),
        ["insta"] = (MediaCategory.SocialMediaImage, 0.70),
        ["facebook"] = (MediaCategory.SocialMediaImage, 0.70),
        ["twitter"] = (MediaCategory.SocialMediaImage, 0.70),
        ["snapchat"] = (MediaCategory.SocialMediaImage, 0.70),
        ["tiktok"] = (MediaCategory.SocialMediaVideo, 0.80),
        ["meme"] = (MediaCategory.Meme, 0.80),
        ["funny"] = (MediaCategory.Meme, 0.50),
        ["lol"] = (MediaCategory.Meme, 0.40)
    };

    // Video extensions
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v", ".3gp", ".flv"
    };

    // Camera make/model signals
    private static readonly string[] CameraMakes =
    [
        "Apple", "Samsung", "Google", "OnePlus", "Xiaomi", "Huawei", "Sony",
        "LG", "Motorola", "Nokia", "OPPO", "Vivo", "Canon", "Nikon",
        "Fujifilm", "Panasonic", "Olympus", "GoPro", "DJI", "Leica"
    ];

    public MediaClassificationService(
        IDbContextFactory<PhotoSortDbContext> contextFactory,
        IPhotoRepository photoRepository,
        ILogger<MediaClassificationService> logger)
    {
        _contextFactory = contextFactory;
        _photoRepository = photoRepository;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(Photo photo, string folderPath)
    {
        var signals = new List<ClassificationSignal>();
        var scores = new Dictionary<MediaCategory, double>();

        // 1. Folder path analysis
        var folderResult = ClassifyByPath(photo.FilePath, folderPath);
        if (folderResult != MediaCategory.Unknown)
        {
            var signal = FolderSignals.FirstOrDefault(kvp =>
                folderPath.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

            if (signal.Key is not null)
            {
                signals.Add(new ClassificationSignal
                {
                    SignalType = "FolderPath",
                    SignalValue = signal.Key,
                    Confidence = signal.Value.Confidence
                });

                AddScore(scores, signal.Value.Category, signal.Value.Confidence);
            }
        }

        // 2. Filename pattern analysis
        var filenameResult = ClassifyByFilename(photo.FileName);
        if (filenameResult != MediaCategory.Unknown)
        {
            // Find the matching prefix signal
            foreach (var prefix in FilenamePrefixSignals)
            {
                if (photo.FileName.StartsWith(prefix.Key, StringComparison.OrdinalIgnoreCase))
                {
                    signals.Add(new ClassificationSignal
                    {
                        SignalType = "FilenamePrefix",
                        SignalValue = prefix.Key,
                        Confidence = prefix.Value.Confidence
                    });

                    AddScore(scores, prefix.Value.Category, prefix.Value.Confidence);
                    break;
                }
            }

            // Find contains signals
            foreach (var contains in FilenameContainsSignals)
            {
                if (photo.FileName.Contains(contains.Key, StringComparison.OrdinalIgnoreCase))
                {
                    signals.Add(new ClassificationSignal
                    {
                        SignalType = "FilenameContains",
                        SignalValue = contains.Key,
                        Confidence = contains.Value.Confidence
                    });

                    AddScore(scores, contains.Value.Category, contains.Value.Confidence);
                }
            }
        }

        // 3. Metadata analysis
        var metadataResult = ClassifyByMetadata(photo);
        if (metadataResult != MediaCategory.Unknown)
        {
            signals.Add(new ClassificationSignal
            {
                SignalType = "Metadata",
                SignalValue = photo.CameraMake ?? "Unknown",
                Confidence = 0.85
            });

            AddScore(scores, metadataResult, 0.85);
        }

        // 4. Determine winner
        var bestCategory = MediaCategory.Unknown;
        var bestScore = 0.0;

        foreach (var kvp in scores)
        {
            if (kvp.Value > bestScore)
            {
                bestScore = kvp.Value;
                bestCategory = kvp.Key;
            }
        }

        // 5. Handle video extension override
        var isVideo = VideoExtensions.Contains(photo.Extension);
        if (isVideo && bestCategory == MediaCategory.Unknown)
        {
            bestCategory = MediaCategory.Unknown;
        }
        else if (isVideo && bestCategory == MediaCategory.CameraPhoto)
        {
            bestCategory = MediaCategory.CameraVideo;
        }
        else if (isVideo && bestCategory == MediaCategory.WhatsAppImage)
        {
            bestCategory = MediaCategory.WhatsAppVideo;
        }
        else if (isVideo && bestCategory == MediaCategory.TelegramImage)
        {
            bestCategory = MediaCategory.TelegramVideo;
        }
        else if (isVideo && bestCategory == MediaCategory.DownloadedImage)
        {
            bestCategory = MediaCategory.DownloadedVideo;
        }
        else if (isVideo && bestCategory == MediaCategory.SocialMediaImage)
        {
            bestCategory = MediaCategory.SocialMediaVideo;
        }

        // 6. Fallback: if no signals but has camera metadata, classify as camera
        if (bestCategory == MediaCategory.Unknown && !string.IsNullOrEmpty(photo.CameraMake))
        {
            bestCategory = isVideo ? MediaCategory.CameraVideo : MediaCategory.CameraPhoto;
            bestScore = 0.60;
            signals.Add(new ClassificationSignal
            {
                SignalType = "FallbackCamera",
                SignalValue = photo.CameraMake,
                Confidence = 0.60
            });
        }

        // 7. Final fallback: Unknown
        if (bestCategory == MediaCategory.Unknown)
        {
            bestScore = 0.0;
        }

        return new ClassificationResult
        {
            PhotoId = photo.Id,
            Category = bestCategory,
            Confidence = Math.Min(bestScore, 1.0),
            Signals = signals.AsReadOnly()
        };
    }

    public async Task<int> ClassifyAllAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        int classified = 0;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var photos = await context.Photos
                .AsNoTracking()
                .Include(p => p.Folder)
                .Where(p => p.ClassificationDate == null)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Classifying {Count} photos", photos.Count);

            int processed = 0;
            foreach (var photo in photos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var folderPath = photo.Folder?.FolderPath ?? string.Empty;
                var result = await ClassifyAsync(photo, folderPath);

                // Update database
                var dbPhoto = await context.Photos.FindAsync(new object[] { photo.Id }, cancellationToken);
                if (dbPhoto is not null)
                {
                    dbPhoto.MediaCategory = result.Category;
                    dbPhoto.ClassificationConfidence = result.Confidence;
                    dbPhoto.ClassificationDate = DateTime.UtcNow;
                }

                classified++;
                processed++;

                if (processed % 100 == 0)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    ProgressChanged?.Invoke(this, processed);
                    _logger.LogDebug("Classified {Processed}/{Total}", processed, photos.Count);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            sw.Stop();
            _logger.LogInformation(
                "Classification completed: {Classified}/{Total} in {Elapsed}ms",
                classified, photos.Count, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Classification cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Classification failed");
        }

        return classified;
    }

    public async Task<IReadOnlyList<CleanupCategory>> GetCleanupCategoriesAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var categoryStats = await context.Photos
                .AsNoTracking()
                .Where(p => p.ClassificationDate != null && p.MediaCategory != MediaCategory.Unknown)
                .GroupBy(p => p.MediaCategory)
                .Select(g => new
                {
                    Category = g.Key,
                    FileCount = g.Count(),
                    TotalSize = g.Sum(p => p.FileSize)
                })
                .ToListAsync();

            var categories = new List<CleanupCategory>();

            foreach (var stat in categoryStats)
            {
                categories.Add(new CleanupCategory
                {
                    Category = stat.Category,
                    DisplayName = GetCategoryDisplayName(stat.Category),
                    Icon = GetCategoryIcon(stat.Category),
                    FileCount = stat.FileCount,
                    TotalSize = stat.TotalSize,
                    PotentialSavings = stat.TotalSize
                });
            }

            return categories.OrderByDescending(c => c.TotalSize).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cleanup categories");
            return [];
        }
    }

    public async Task<IReadOnlyList<GalleryPhoto>> GetPhotosByCategoryAsync(
        MediaCategory category, int skip = 0, int take = 100)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Photos
                .AsNoTracking()
                .Where(p => p.MediaCategory == category)
                .OrderByDescending(p => p.DateTaken ?? p.ModifiedDateUtc)
                .Skip(skip)
                .Take(take)
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
                    DateTakenYear = p.DateTaken != null ? p.DateTaken.Value.Year : (int?)null,
                    DateTakenMonth = p.DateTaken != null ? p.DateTaken.Value.Month : (int?)null,
                    DateTakenDay = p.DateTaken != null ? p.DateTaken.Value.Day : (int?)null
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get photos for category {Category}", category);
            return [];
        }
    }

    public async Task<CleanupStatistics> GetStatisticsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var totalFiles = await context.Photos.CountAsync();
            var classifiedFiles = await context.Photos.CountAsync(p => p.ClassificationDate != null);
            var totalSize = await context.Photos.SumAsync(p => p.FileSize);
            var classifiableSize = await context.Photos
                .Where(p => p.ClassificationDate != null && p.MediaCategory != MediaCategory.Unknown)
                .SumAsync(p => p.FileSize);

            return new CleanupStatistics
            {
                TotalFiles = totalFiles,
                ClassifiedFiles = classifiedFiles,
                UnclassifiedFiles = totalFiles - classifiedFiles,
                TotalSize = totalSize,
                ClassifiableSize = classifiableSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cleanup statistics");
            return new CleanupStatistics();
        }
    }

    public async Task DeletePhotosByCategoryAsync(MediaCategory category, IReadOnlyList<int> photoIds)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var photos = await context.Photos
                .Where(p => photoIds.Contains(p.Id) && p.MediaCategory == category)
                .ToListAsync();

            int deleted = 0;
            foreach (var photo in photos)
            {
                try
                {
                    if (File.Exists(photo.FilePath))
                    {
                        File.Delete(photo.FilePath);
                    }

                    context.Photos.Remove(photo);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete {FilePath}", photo.FilePath);
                }
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("Deleted {Deleted} photos from category {Category}", deleted, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete photos for category {Category}", category);
        }
    }

    public MediaCategory ClassifyByPath(string filePath, string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return MediaCategory.Unknown;

        var normalizedPath = folderPath.Replace('\\', '/').ToLowerInvariant();

        foreach (var signal in FolderSignals)
        {
            if (normalizedPath.Contains(signal.Key.ToLowerInvariant()))
            {
                return signal.Value.Category;
            }
        }

        return MediaCategory.Unknown;
    }

    public MediaCategory ClassifyByFilename(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return MediaCategory.Unknown;

        var lowerName = fileName.ToLowerInvariant();

        // Check prefix signals
        foreach (var signal in FilenamePrefixSignals)
        {
            if (lowerName.StartsWith(signal.Key.ToLowerInvariant()))
            {
                return signal.Value.Category;
            }
        }

        // Check contains signals
        foreach (var signal in FilenameContainsSignals)
        {
            if (lowerName.Contains(signal.Key.ToLowerInvariant()))
            {
                return signal.Value.Category;
            }
        }

        return MediaCategory.Unknown;
    }

    public MediaCategory ClassifyByMetadata(Photo photo)
    {
        // If has camera make/model, it's likely a camera photo
        if (!string.IsNullOrEmpty(photo.CameraMake) || !string.IsNullOrEmpty(photo.CameraModel))
        {
            var isVideo = VideoExtensions.Contains(photo.Extension);
            return isVideo ? MediaCategory.CameraVideo : MediaCategory.CameraPhoto;
        }

        // If has GPS, likely camera
        if (photo.Latitude.HasValue && photo.Longitude.HasValue)
        {
            var isVideo = VideoExtensions.Contains(photo.Extension);
            return isVideo ? MediaCategory.CameraVideo : MediaCategory.CameraPhoto;
        }

        return MediaCategory.Unknown;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    private static void AddScore(Dictionary<MediaCategory, double> scores, MediaCategory category, double confidence)
    {
        if (scores.TryGetValue(category, out var existing))
        {
            scores[category] = Math.Max(existing, confidence);
        }
        else
        {
            scores[category] = confidence;
        }
    }

    private static string GetCategoryDisplayName(MediaCategory category)
    {
        return category switch
        {
            MediaCategory.CameraPhoto => "Camera Photos",
            MediaCategory.CameraVideo => "Camera Videos",
            MediaCategory.WhatsAppImage => "WhatsApp Images",
            MediaCategory.WhatsAppVideo => "WhatsApp Videos",
            MediaCategory.TelegramImage => "Telegram Images",
            MediaCategory.TelegramVideo => "Telegram Videos",
            MediaCategory.Screenshot => "Screenshots",
            MediaCategory.ScreenRecording => "Screen Recordings",
            MediaCategory.DownloadedImage => "Downloaded Images",
            MediaCategory.DownloadedVideo => "Downloaded Videos",
            MediaCategory.SocialMediaImage => "Social Media Images",
            MediaCategory.SocialMediaVideo => "Social Media Videos",
            MediaCategory.Meme => "Memes",
            MediaCategory.Unknown => "Unknown",
            _ => "Other"
        };
    }

    private static string GetCategoryIcon(MediaCategory category)
    {
        return category switch
        {
            MediaCategory.CameraPhoto => "\U0001F4F7",
            MediaCategory.CameraVideo => "\U0001F3AC",
            MediaCategory.WhatsAppImage => "\U0001F4F1",
            MediaCategory.WhatsAppVideo => "\U0001F4F9",
            MediaCategory.TelegramImage => "\u2708",
            MediaCategory.TelegramVideo => "\U0001F4F9",
            MediaCategory.Screenshot => "\U0001F5B1",
            MediaCategory.ScreenRecording => "\U0001F3AC",
            MediaCategory.DownloadedImage => "\u2B07",
            MediaCategory.DownloadedVideo => "\u2B07",
            MediaCategory.SocialMediaImage => "\U0001F4F1",
            MediaCategory.SocialMediaVideo => "\U0001F4F9",
            MediaCategory.Meme => "\U0001F602",
            MediaCategory.Unknown => "\u2753",
            _ => "\u2753"
        };
    }
}
