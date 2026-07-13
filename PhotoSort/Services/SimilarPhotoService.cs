using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class SimilarPhotoService : ISimilarPhotoService
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly IPhotoRepository _photoRepository;
    private readonly IThumbnailService _thumbnailService;
    private readonly ILogger<SimilarPhotoService> _logger;

    private readonly CancellationTokenSource _cts;
    private readonly ManualResetEventSlim _pauseEvent;
    private readonly object _progressLock;
    private readonly List<SimilarPhotoGroup> _results;

    private SimilarPhotoDetectionProgress _progress;
    private Task? _detectionTask;
    private bool _disposed;

    public event EventHandler<SimilarPhotoDetectionProgress>? ProgressChanged;
    event EventHandler<IReadOnlyList<SimilarPhotoGroup>>? ISimilarPhotoService.DetectionCompleted
    {
        add => _detectionCompleted += value;
        remove => _detectionCompleted -= value;
    }
    private event EventHandler<IReadOnlyList<SimilarPhotoGroup>>? _detectionCompleted;

    public bool IsRunning => _detectionTask is { IsCompleted: false };

    private const int HashBatchSize = 100;
    private const int ComparisonBatchSize = 500;
    private const int ProgressReportInterval = 500;

    // Similarity thresholds (Hamming Distance)
    private const int ExactMatchThreshold = 0;
    private const int VerySimilarThreshold = 5;
    private const int SimilarThreshold = 10;

    public SimilarPhotoService(
        IDbContextFactory<PhotoSortDbContext> contextFactory,
        IPhotoRepository photoRepository,
        IThumbnailService thumbnailService,
        ILogger<SimilarPhotoService> logger)
    {
        _contextFactory = contextFactory;
        _photoRepository = photoRepository;
        _thumbnailService = thumbnailService;
        _logger = logger;

        _cts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true);
        _progressLock = new object();
        _results = [];

        _progress = new SimilarPhotoDetectionProgress
        {
            Phase = SimilarDetectionPhase.Idle
        };
    }

    public async Task StartDetectionAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        _results.Clear();

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _detectionTask = Task.Run(() => RunDetectionAsync(linkedCts.Token), linkedCts.Token);

        try
        {
            await _detectionTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Similar photo detection failed");
        }
    }

    public void PauseDetection()
    {
        if (IsRunning)
        {
            _pauseEvent.Reset();
            UpdatePhase(SimilarDetectionPhase.Paused);
        }
    }

    public void ResumeDetection()
    {
        if (!IsRunning)
            return;

        _pauseEvent.Set();

        lock (_progressLock)
        {
            if (_progress.Phase == SimilarDetectionPhase.Paused)
            {
                _progress.Phase = SimilarDetectionPhase.HashGeneration;
                FireProgressChanged();
            }
        }
    }

    public void CancelDetection()
    {
        _pauseEvent.Set();
        _cts.Cancel();
    }

    public SimilarPhotoDetectionProgress GetProgress()
    {
        lock (_progressLock)
        {
            return _progress;
        }
    }

    public IReadOnlyList<SimilarPhotoGroup> GetResults()
    {
        lock (_progressLock)
        {
            return _results.AsReadOnly();
        }
    }

    public async Task<IReadOnlyList<SimilarPhotoGroup>> GetSimilarGroupsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Get all photos with similar group IDs (client-side to avoid translation issues)
            var allPhotos = await context.Photos
                .AsNoTracking()
                .Where(p => p.SimilarPhotoGroupId != null && p.PerceptualHash != null)
                .Select(p => new { p.Id, p.FilePath, p.FileName, p.FileSize, p.DateTaken,
                                   p.ThumbnailSmallPath, p.SimilarPhotoGroupId, p.PerceptualHash,
                                   p.Width, p.Height,
                                   FolderPath = p.Folder != null ? p.Folder.FolderPath : null })
                .ToListAsync();

            // Group in memory
            var groups = allPhotos
                .GroupBy(p => p.SimilarPhotoGroupId)
                .Select(g => new
                {
                    GroupId = g.Key!.Value,
                    Photos = g.Select(p => new SimilarPhotoItem
                    {
                        Id = p.Id,
                        FilePath = p.FilePath,
                        FileName = p.FileName,
                        FileSize = p.FileSize,
                        DateTaken = p.DateTaken,
                        ThumbnailSmallPath = p.ThumbnailSmallPath,
                        FolderPath = p.FolderPath,
                        PerceptualHash = p.PerceptualHash!.Value,
                        Width = p.Width,
                        Height = p.Height
                    }).ToList()
                })
                .Where(g => g.Photos.Count > 1)
                .OrderByDescending(g => g.Photos.Count)
                .ToList();

            var result = new List<SimilarPhotoGroup>();
            foreach (var group in groups)
            {
                var photos = group.Photos;
                var best = photos.OrderByDescending(p => p.Width ?? 0)
                    .ThenByDescending(p => p.Height ?? 0)
                    .ThenByDescending(p => p.FileSize)
                    .First();
                best.IsBest = true;

                var referenceHash = best.PerceptualHash;
                var maxDistance = photos.Max(p => ComputeHammingDistance(referenceHash, p.PerceptualHash));

                var level = maxDistance switch
                {
                    0 => SimilarityLevel.ExactMatch,
                    <= VerySimilarThreshold => SimilarityLevel.VerySimilar,
                    _ => SimilarityLevel.Similar
                };

                var similarGroup = new SimilarPhotoGroup
                {
                    GroupId = group.GroupId,
                    ReferenceHash = referenceHash,
                    Level = level,
                    BestPhoto = new GalleryPhoto
                    {
                        Id = best.Id,
                        FilePath = best.FilePath,
                        FileName = best.FileName,
                        Extension = System.IO.Path.GetExtension(best.FilePath),
                        FileSize = best.FileSize,
                        DateTaken = best.DateTaken,
                        ThumbnailSmallPath = best.ThumbnailSmallPath,
                        Width = best.Width,
                        Height = best.Height,
                        ModifiedDateUtc = DateTime.UtcNow,
                        FolderId = 0
                    },
                    FileSize = best.FileSize
                };

                foreach (var photo in photos.Where(p => p.Id != best.Id))
                {
                    photo.HammingDistance = ComputeHammingDistance(referenceHash, photo.PerceptualHash);
                    similarGroup.SimilarPhotos.Add(photo);
                }

                result.Add(similarGroup);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get similar groups from database");
            return [];
        }
    }

    public async Task DeletePhotoAsync(int photoId)
    {
        try
        {
            var photo = await _photoRepository.GetByIdAsync(photoId);
            if (photo is null)
                return;

            if (System.IO.File.Exists(photo.FilePath))
            {
                System.IO.File.Delete(photo.FilePath);
            }

            await _photoRepository.DeleteAsync(photo);

            _logger.LogInformation("Deleted similar photo: {FilePath}", photo.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete photo {PhotoId}", photoId);
        }
    }

    public async Task IgnoreGroupAsync(int groupId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var photos = await context.Photos
                .Where(p => p.SimilarPhotoGroupId == groupId)
                .ToListAsync();

            foreach (var photo in photos)
            {
                photo.SimilarPhotoGroupId = null;
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("Ignored similar photo group {GroupId}", groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ignore group {GroupId}", groupId);
        }
    }

    public int ComputeHammingDistance(ulong hash1, ulong hash2)
    {
        ulong xor = hash1 ^ hash2;
        int count = 0;

        while (xor != 0)
        {
            count++;
            xor &= xor - 1;
        }

        return count;
    }

    public ulong ComputePerceptualHash(byte[] imageData)
    {
        try
        {
            using var stream = new MemoryStream(imageData);
            using var bitmap = new Bitmap(stream);

            // Resize to 8x8
            using var resized = new Bitmap(8, 8);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(bitmap, 0, 0, 8, 8);
            }

            // Convert to grayscale and compute mean
            double totalLuminance = 0;
            var pixels = new double[8, 8];

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    var pixel = resized.GetPixel(x, y);
                    // Luminance formula: 0.299*R + 0.587*G + 0.114*B
                    double luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;
                    pixels[x, y] = luminance;
                    totalLuminance += luminance;
                }
            }

            double meanLuminance = totalLuminance / 64.0;

            // Generate 64-bit hash
            ulong hash = 0;
            int bitIndex = 0;

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    if (pixels[x, y] > meanLuminance)
                    {
                        hash |= (1UL << bitIndex);
                    }
                    bitIndex++;
                }
            }

            return hash;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _pauseEvent.Dispose();
    }

    private async Task RunDetectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Phase 1: Scan thumbnails
            UpdatePhase(SimilarDetectionPhase.ThumbnailScan);
            var thumbnailPhotos = await ScanThumbnailsAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            // Phase 2: Generate perceptual hashes
            UpdatePhase(SimilarDetectionPhase.HashGeneration);
            var hashedPhotos = await GenerateHashesAsync(thumbnailPhotos, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            // Phase 3: Compare hashes
            UpdatePhase(SimilarDetectionPhase.Comparison);
            var comparisons = await CompareHashesAsync(hashedPhotos, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            // Phase 4: Group similar photos
            UpdatePhase(SimilarDetectionPhase.Grouping);
            var groups = GroupSimilarPhotos(comparisons);

            // Save to database
            await SaveGroupsToDatabaseAsync(groups, cancellationToken);

            lock (_progressLock)
            {
                _results.Clear();
                _results.AddRange(groups);
            }

            UpdatePhase(SimilarDetectionPhase.Completed);

            _detectionCompleted?.Invoke(this, groups);

            _logger.LogInformation(
                "Similar photo detection completed. Found {GroupCount} groups with {TotalSimilar} similar photos",
                groups.Count, groups.Sum(g => g.SimilarPhotos.Count));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Similar photo detection was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Similar photo detection failed");
        }
    }

    private async Task<List<(int Id, string FilePath, long FileSize, DateTime? DateTaken, string? ThumbnailPath, int? Width, int? Height)>> ScanThumbnailsAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var photos = await context.Photos
            .AsNoTracking()
            .Where(p => p.ThumbnailSmallPath != null && p.Extension != ".mp4" && p.Extension != ".mov" && p.Extension != ".avi")
            .Select(p => new
            {
                p.Id,
                p.FilePath,
                p.FileSize,
                p.DateTaken,
                p.ThumbnailSmallPath,
                p.Width,
                p.Height
            })
            .ToListAsync(cancellationToken);

        var result = photos
            .Where(p => p.ThumbnailSmallPath != null && File.Exists(p.ThumbnailSmallPath))
            .Select(p => (p.Id, p.FilePath, p.FileSize, p.DateTaken, p.ThumbnailSmallPath, p.Width, p.Height))
            .ToList();

        lock (_progressLock)
        {
            _progress.TotalPhotos = result.Count;
            _progress.ThumbnailsAvailable = result.Count;
            FireProgressChanged();
        }

        _logger.LogInformation("Found {Count} photos with available thumbnails", result.Count);

        return result;
    }

    private async Task<List<(int Id, string FilePath, long FileSize, DateTime? DateTaken, ulong Hash, int? Width, int? Height)>> GenerateHashesAsync(
        List<(int Id, string FilePath, long FileSize, DateTime? DateTaken, string? ThumbnailPath, int? Width, int? Height)> photos,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<(int Id, string FilePath, long FileSize, DateTime? DateTaken, ulong Hash, int? Width, int? Height)>();
        int processed = 0;

        // Get photos that already have hashes
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existingHashes = await context.Photos
            .AsNoTracking()
            .Where(p => p.PerceptualHash != null)
            .ToDictionaryAsync(p => p.Id, p => p.PerceptualHash!.Value, cancellationToken);

        var photosToHash = photos.Where(p => !existingHashes.ContainsKey(p.Id)).ToList();

        // Add already hashed photos to results
        foreach (var photo in photos.Where(p => existingHashes.ContainsKey(p.Id)))
        {
            results.Add((photo.Id, photo.FilePath, photo.FileSize, photo.DateTaken, existingHashes[photo.Id], photo.Width, photo.Height));
        }

        lock (_progressLock)
        {
            _progress.HashesGenerated = results.Count;
            FireProgressChanged();
        }

        var batches = photosToHash
            .Select((p, i) => new { p, i })
            .GroupBy(x => x.i / HashBatchSize)
            .Select(g => g.Select(x => x.p).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseEvent.Wait(cancellationToken);

            var tasks = batch.Select(async photo =>
            {
                try
                {
                    if (!File.Exists(photo.ThumbnailPath))
                        return;

                    var imageData = await File.ReadAllBytesAsync(photo.ThumbnailPath, cancellationToken);
                    var hash = ComputePerceptualHash(imageData);

                    if (hash != 0)
                    {
                        results.Add((photo.Id, photo.FilePath, photo.FileSize, photo.DateTaken, hash, photo.Width, photo.Height));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to generate hash for {FilePath}", photo.FilePath);
                }
            });

            await Task.WhenAll(tasks);

            processed += batch.Count;

            if (processed % ProgressReportInterval == 0)
            {
                lock (_progressLock)
                {
                    _progress.HashesGenerated = results.Count;
                    FireProgressChanged();
                }
            }
        }

        lock (_progressLock)
        {
            _progress.HashesGenerated = results.Count;
            FireProgressChanged();
        }

        _logger.LogInformation("Generated {Count} perceptual hashes", results.Count);

        return results.ToList();
    }

    private async Task<List<(int Id1, string FilePath1, long FileSize1, DateTime? DateTaken1, ulong Hash1, int? Width1, int? Height1,
                             int Id2, string FilePath2, long FileSize2, DateTime? DateTaken2, ulong Hash2, int? Width2, int? Height2,
                             int Distance)>> CompareHashesAsync(
        List<(int Id, string FilePath, long FileSize, DateTime? DateTaken, ulong Hash, int? Width, int? Height)> hashedPhotos,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<(int Id1, string FilePath1, long FileSize1, DateTime? DateTaken1, ulong Hash1, int? Width1, int? Height1,
                                        int Id2, string FilePath2, long FileSize2, DateTime? DateTaken2, ulong Hash2, int? Width2, int? Height2,
                                        int Distance)>();

        int compared = 0;

        // Sort by hash for better locality
        var sorted = hashedPhotos.OrderBy(p => p.Hash).ToList();

        // Compare in batches for better performance
        var batches = sorted
            .Select((p, i) => new { p, i })
            .GroupBy(x => x.i / ComparisonBatchSize)
            .Select(g => g.Select(x => x.p).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseEvent.Wait(cancellationToken);

            // Compare each photo in batch with all others
            foreach (var photo1 in batch)
            {
                foreach (var photo2 in sorted.Where(p => p.Id > photo1.Id))
                {
                    int distance = ComputeHammingDistance(photo1.Hash, photo2.Hash);

                    if (distance <= SimilarThreshold)
                    {
                        results.Add((
                            photo1.Id, photo1.FilePath, photo1.FileSize, photo1.DateTaken, photo1.Hash, photo1.Width, photo1.Height,
                            photo2.Id, photo2.FilePath, photo2.FileSize, photo2.DateTaken, photo2.Hash, photo2.Width, photo2.Height,
                            distance));
                    }
                }

                compared++;

                if (compared % ProgressReportInterval == 0)
                {
                    lock (_progressLock)
                    {
                        _progress.PhotosCompared = compared;
                        FireProgressChanged();
                    }
                }
            }
        }

        lock (_progressLock)
        {
            _progress.PhotosCompared = compared;
            FireProgressChanged();
        }

        _logger.LogInformation("Compared {Count} photo pairs", results.Count);

        return results.ToList();
    }

    private List<SimilarPhotoGroup> GroupSimilarPhotos(
        List<(int Id1, string FilePath1, long FileSize1, DateTime? DateTaken1, ulong Hash1, int? Width1, int? Height1,
              int Id2, string FilePath2, long FileSize2, DateTime? DateTaken2, ulong Hash2, int? Width2, int? Height2,
              int Distance)> comparisons)
    {
        // Union-Find for grouping
        var parent = new Dictionary<int, int>();
        var rank = new Dictionary<int, int>();

        int Find(int x)
        {
            if (!parent.ContainsKey(x))
            {
                parent[x] = x;
                rank[x] = 0;
            }

            if (parent[x] != x)
            {
                parent[x] = Find(parent[x]);
            }

            return parent[x];
        }

        void Union(int x, int y)
        {
            int rootX = Find(x);
            int rootY = Find(y);

            if (rootX == rootY)
                return;

            if (rank[rootX] < rank[rootY])
            {
                parent[rootX] = rootY;
            }
            else if (rank[rootX] > rank[rootY])
            {
                parent[rootY] = rootX;
            }
            else
            {
                parent[rootY] = rootX;
                rank[rootX]++;
            }
        }

        // Group by union-find
        foreach (var comp in comparisons)
        {
            Union(comp.Id1, comp.Id2);
        }

        // Build groups
        var groupMap = new Dictionary<int, List<(int Id, string FilePath, long FileSize, DateTime? DateTaken, ulong Hash, int? Width, int? Height, int Distance)>>();
        var photoLookup = new Dictionary<int, (string FilePath, long FileSize, DateTime? DateTaken, ulong Hash, int? Width, int? Height)>();

        foreach (var comp in comparisons)
        {
            photoLookup[comp.Id1] = (comp.FilePath1, comp.FileSize1, comp.DateTaken1, comp.Hash1, comp.Width1, comp.Height1);
            photoLookup[comp.Id2] = (comp.FilePath2, comp.FileSize2, comp.DateTaken2, comp.Hash2, comp.Width2, comp.Height2);
        }

        foreach (var comp in comparisons)
        {
            int root = Find(comp.Id1);

            if (!groupMap.ContainsKey(root))
            {
                groupMap[root] = [];
            }

            if (!groupMap[root].Any(p => p.Id == comp.Id1))
            {
                groupMap[root].Add((comp.Id1, comp.FilePath1, comp.FileSize1, comp.DateTaken1, comp.Hash1, comp.Width1, comp.Height1, comp.Distance));
            }

            if (!groupMap[root].Any(p => p.Id == comp.Id2))
            {
                groupMap[root].Add((comp.Id2, comp.FilePath2, comp.FileSize2, comp.DateTaken2, comp.Hash2, comp.Width2, comp.Height2, comp.Distance));
            }
        }

        // Convert to SimilarPhotoGroup
        var groups = groupMap.Values
            .Where(g => g.Count > 1)
            .Select((photos, index) =>
            {
                var best = photos.OrderByDescending(p => p.Width ?? 0)
                    .ThenByDescending(p => p.Height ?? 0)
                    .ThenByDescending(p => p.FileSize)
                    .First();

                var referenceHash = best.Hash;
                var maxDistance = photos.Max(p => ComputeHammingDistance(referenceHash, p.Hash));

                var level = maxDistance switch
                {
                    0 => SimilarityLevel.ExactMatch,
                    <= VerySimilarThreshold => SimilarityLevel.VerySimilar,
                    _ => SimilarityLevel.Similar
                };

                var group = new SimilarPhotoGroup
                {
                    GroupId = index + 1,
                    ReferenceHash = referenceHash,
                    Level = level,
                    BestPhoto = new GalleryPhoto
                    {
                        Id = best.Id,
                        FilePath = best.FilePath,
                        FileName = System.IO.Path.GetFileName(best.FilePath),
                        Extension = System.IO.Path.GetExtension(best.FilePath),
                        FileSize = best.FileSize,
                        DateTaken = best.DateTaken,
                        ThumbnailSmallPath = _thumbnailService.GetThumbnailPath(best.Id, ThumbnailSize.Small),
                        Width = best.Width,
                        Height = best.Height,
                        ModifiedDateUtc = DateTime.UtcNow,
                        FolderId = 0
                    },
                    FileSize = best.FileSize
                };

                foreach (var photo in photos.Where(p => p.Id != best.Id))
                {
                    group.SimilarPhotos.Add(new SimilarPhotoItem
                    {
                        Id = photo.Id,
                        FilePath = photo.FilePath,
                        FileName = System.IO.Path.GetFileName(photo.FilePath),
                        FileSize = photo.FileSize,
                        DateTaken = photo.DateTaken,
                        ThumbnailSmallPath = _thumbnailService.GetThumbnailPath(photo.Id, ThumbnailSize.Small),
                        PerceptualHash = photo.Hash,
                        HammingDistance = ComputeHammingDistance(referenceHash, photo.Hash),
                        Width = photo.Width,
                        Height = photo.Height
                    });
                }

                return group;
            })
            .OrderByDescending(g => g.GroupSize)
            .ToList();

        lock (_progressLock)
        {
            _progress.GroupsFound = groups.Count;
            _progress.ReclaimableBytes = groups.Sum(g => g.PotentialSavings);
            FireProgressChanged();
        }

        return groups;
    }

    private async Task SaveGroupsToDatabaseAsync(
        List<SimilarPhotoGroup> groups,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Mark best photo
                if (group.BestPhoto is not null)
                {
                    var best = await context.Photos.FindAsync(
                        new object[] { group.BestPhoto.Id }, cancellationToken);

                    if (best is not null)
                    {
                        best.SimilarPhotoGroupId = group.GroupId;
                        best.PerceptualHash = group.ReferenceHash;
                        best.PerceptualHashDate = DateTime.UtcNow;
                    }
                }

                // Mark similar photos
                foreach (var similar in group.SimilarPhotos)
                {
                    var photo = await context.Photos.FindAsync(
                        new object[] { similar.Id }, cancellationToken);

                    if (photo is not null)
                    {
                        photo.SimilarPhotoGroupId = group.GroupId;
                        photo.PerceptualHash = similar.PerceptualHash;
                        photo.PerceptualHashDate = DateTime.UtcNow;
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Saved {GroupCount} similar photo groups to database", groups.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save similar photo groups to database");
        }
    }

    private void UpdatePhase(SimilarDetectionPhase phase)
    {
        lock (_progressLock)
        {
            _progress.Phase = phase;
            FireProgressChanged();
        }
    }

    private void FireProgressChanged()
    {
        ProgressChanged?.Invoke(this, _progress);
    }
}
