using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class DuplicateDetectionService : IDuplicateDetectionService
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly IPhotoRepository _photoRepository;
    private readonly ILogger<DuplicateDetectionService> _logger;

    private readonly CancellationTokenSource _cts;
    private readonly ManualResetEventSlim _pauseEvent;
    private readonly object _progressLock;
    private readonly List<DuplicateGroup> _results;

    private DuplicateDetectionProgress _progress;
    private Task? _detectionTask;
    private bool _disposed;

    public event EventHandler<DuplicateDetectionProgress>? ProgressChanged;
    event EventHandler<IReadOnlyList<DuplicateGroup>>? IDuplicateDetectionService.DetectionCompleted
    {
        add => _detectionCompleted += value;
        remove => _detectionCompleted -= value;
    }
    private event EventHandler<IReadOnlyList<DuplicateGroup>>? _detectionCompleted;

    public bool IsRunning => _detectionTask is { IsCompleted: false };

    private const int HashBatchSize = 50;
    private const int ProgressReportInterval = 100;

    public DuplicateDetectionService(
        IDbContextFactory<PhotoSortDbContext> contextFactory,
        IPhotoRepository photoRepository,
        ILogger<DuplicateDetectionService> logger)
    {
        _contextFactory = contextFactory;
        _photoRepository = photoRepository;
        _logger = logger;

        _cts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true);
        _progressLock = new object();
        _results = [];

        _progress = new DuplicateDetectionProgress
        {
            Phase = DuplicateDetectionPhase.Idle
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
            _logger.LogError(ex, "Duplicate detection failed");
        }
    }

    public void PauseDetection()
    {
        if (IsRunning)
        {
            _pauseEvent.Reset();
            UpdatePhase(DuplicateDetectionPhase.Paused);
        }
    }

    public void ResumeDetection()
    {
        if (!IsRunning)
            return;

        _pauseEvent.Set();

        lock (_progressLock)
        {
            if (_progress.Phase == DuplicateDetectionPhase.Paused)
            {
                _progress.Phase = DuplicateDetectionPhase.CandidateIdentification;
                FireProgressChanged();
            }
        }
    }

    public void CancelDetection()
    {
        _pauseEvent.Set();
        _cts.Cancel();
    }

    public DuplicateDetectionProgress GetProgress()
    {
        lock (_progressLock)
        {
            return _progress;
        }
    }

    public IReadOnlyList<DuplicateGroup> GetResults()
    {
        lock (_progressLock)
        {
            return _results.AsReadOnly();
        }
    }

    public async Task<IReadOnlyList<DuplicateGroup>> GetDuplicateGroupsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Get all photos with duplicate group IDs (client-side to avoid translation issues)
            var allPhotos = await context.Photos
                .AsNoTracking()
                .Where(p => p.DuplicateGroupId != null && p.ContentHash != null)
                .Select(p => new { p.Id, p.FilePath, p.FileName, p.FileSize, p.DateTaken,
                                   p.ThumbnailSmallPath, p.DuplicateGroupId, p.ContentHash,
                                   FolderPath = p.Folder != null ? p.Folder.FolderPath : null })
                .ToListAsync();

            // Group in memory
            var groups = allPhotos
                .GroupBy(p => new { p.DuplicateGroupId, p.ContentHash })
                .Select(g => new
                {
                    GroupId = g.Key.DuplicateGroupId!.Value,
                    Hash = g.Key.ContentHash!,
                    Photos = g.Select(p => new DuplicatePhoto
                    {
                        Id = p.Id,
                        FilePath = p.FilePath,
                        FileName = p.FileName,
                        FileSize = p.FileSize,
                        DateTaken = p.DateTaken,
                        ThumbnailSmallPath = p.ThumbnailSmallPath,
                        FolderPath = p.FolderPath,
                        IsOriginal = false
                    }).ToList()
                })
                .Where(g => g.Photos.Count > 1)
                .OrderByDescending(g => g.Photos.Count)
                .ToList();

            var result = new List<DuplicateGroup>();
            foreach (var group in groups)
            {
                var photos = group.Photos;
                var original = photos.OrderByDescending(p => p.DateTaken).ThenByDescending(p => p.Id).First();
                original.IsOriginal = true;

                var duplicateGroup = new DuplicateGroup
                {
                    GroupId = group.GroupId,
                    ContentHash = group.Hash,
                    OriginalPhoto = new GalleryPhoto
                    {
                        Id = original.Id,
                        FilePath = original.FilePath,
                        FileName = original.FileName,
                        Extension = System.IO.Path.GetExtension(original.FilePath),
                        DateTaken = original.DateTaken,
                        FileSize = original.FileSize,
                        ThumbnailSmallPath = original.ThumbnailSmallPath,
                        ModifiedDateUtc = DateTime.UtcNow,
                        FolderId = 0
                    },
                    FileSize = original.FileSize
                };

                foreach (var photo in photos.Where(p => p.Id != original.Id))
                {
                    duplicateGroup.Duplicates.Add(photo);
                }

                result.Add(duplicateGroup);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get duplicate groups from database");
            return [];
        }
    }

    public async Task DeleteDuplicateAsync(int photoId)
    {
        try
        {
            var photo = await _photoRepository.GetByIdAsync(photoId);
            if (photo is null)
                return;

            if (File.Exists(photo.FilePath))
            {
                File.Delete(photo.FilePath);
            }

            await _photoRepository.DeleteAsync(photo);

            _logger.LogInformation("Deleted duplicate photo: {FilePath}", photo.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete duplicate photo {PhotoId}", photoId);
        }
    }

    public async Task MarkAsOriginalAsync(int groupId, int newOriginalPhotoId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var groupPhotos = await context.Photos
                .Where(p => p.DuplicateGroupId == groupId)
                .ToListAsync();

            foreach (var photo in groupPhotos)
            {
                if (photo.Id == newOriginalPhotoId)
                {
                    photo.DuplicateGroupId = null;
                }
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("Marked photo {PhotoId} as original in group {GroupId}",
                newOriginalPhotoId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark photo {PhotoId} as original", newOriginalPhotoId);
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
            // Phase 1: Identify candidates (files with same size AND same dimensions)
            UpdatePhase(DuplicateDetectionPhase.CandidateIdentification);
            var candidates = await IdentifyCandidatesAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            // Phase 2: Compute hashes for candidates
            UpdatePhase(DuplicateDetectionPhase.HashComputation);
            var hashedCandidates = await ComputeHashesAsync(candidates, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            // Phase 3: Group by hash
            UpdatePhase(DuplicateDetectionPhase.Grouping);
            var groups = GroupDuplicates(hashedCandidates);

            // Save to database
            await SaveGroupsToDatabaseAsync(groups, cancellationToken);

            lock (_progressLock)
            {
                _results.Clear();
                _results.AddRange(groups);
            }

            UpdatePhase(DuplicateDetectionPhase.Completed);

            _detectionCompleted?.Invoke(this, groups);

            _logger.LogInformation(
                "Duplicate detection completed. Found {GroupCount} groups with {TotalDuplicates} duplicates",
                groups.Count, groups.Sum(g => g.DuplicateCount));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Duplicate detection was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Duplicate detection failed");
        }
    }

    private async Task<List<(int Id, string FilePath, long FileSize, int? Width, int? Height)>> IdentifyCandidatesAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Step 1: Get file sizes that appear more than once (client-side to avoid translation issues)
        var allPhotos = await context.Photos
            .AsNoTracking()
            .Where(p => p.FileSize > 0)
            .Select(p => new { p.Id, p.FilePath, p.FileSize, p.Width, p.Height })
            .ToListAsync(cancellationToken);

        // Step 2: Group by file size in memory
        var candidates = allPhotos
            .GroupBy(p => p.FileSize)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        // Further filter by width/height if available
        var dimensionGroups = candidates
            .Where(c => c.Width.HasValue && c.Height.HasValue)
            .GroupBy(c => new { c.Width, c.Height, c.FileSize })
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        // Also include files with same size but no dimensions (could be videos)
        var noDimensionCandidates = candidates
            .Where(c => !c.Width.HasValue || !c.Height.HasValue)
            .GroupBy(c => c.FileSize)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        var allCandidates = dimensionGroups
            .Concat(noDimensionCandidates)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();

        lock (_progressLock)
        {
            _progress.TotalFiles = context.Photos.Count();
            _progress.CandidatesIdentified = allCandidates.Count;
            FireProgressChanged();
        }

        _logger.LogInformation("Identified {Count} candidate files for hash comparison", allCandidates.Count);

        return allCandidates.Select(c => (c.Id, c.FilePath, c.FileSize, c.Width, c.Height)).ToList();
    }

    private async Task<List<(int Id, string FilePath, long FileSize, string Hash)>> ComputeHashesAsync(
        List<(int Id, string FilePath, long FileSize, int? Width, int? Height)> candidates,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<(int Id, string FilePath, long FileSize, string Hash)>();
        int processed = 0;

        var batches = candidates
            .Select((c, i) => new { c, i })
            .GroupBy(x => x.i / HashBatchSize)
            .Select(g => g.Select(x => x.c).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseEvent.Wait(cancellationToken);

            var tasks = batch.Select(async candidate =>
            {
                try
                {
                    if (!File.Exists(candidate.FilePath))
                        return;

                    var hash = await ComputeFileHashAsync(candidate.FilePath, cancellationToken);
                    if (hash is not null)
                    {
                        results.Add((candidate.Id, candidate.FilePath, candidate.FileSize, hash));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to hash file: {FilePath}", candidate.FilePath);
                }
            });

            await Task.WhenAll(tasks);

            processed += batch.Count;

            if (processed % ProgressReportInterval == 0)
            {
                lock (_progressLock)
                {
                    _progress.FilesProcessed = processed;
                    _progress.HashesComputed = results.Count;
                    FireProgressChanged();
                }
            }
        }

        lock (_progressLock)
        {
            _progress.FilesProcessed = candidates.Count;
            _progress.HashesComputed = results.Count;
            FireProgressChanged();
        }

        return results.ToList();
    }

    private static async Task<string?> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var sha256 = SHA256.Create();
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private List<DuplicateGroup> GroupDuplicates(
        List<(int Id, string FilePath, long FileSize, string Hash)> hashedFiles)
    {
        var groups = hashedFiles
            .GroupBy(f => f.Hash)
            .Where(g => g.Count() > 1)
            .Select((g, index) =>
            {
                var files = g.OrderByDescending(f => f.FileSize).ToList();
                var original = files.First();

                var group = new DuplicateGroup
                {
                    GroupId = index + 1,
                    ContentHash = g.Key,
                    FileSize = original.FileSize,
                    OriginalPhoto = new GalleryPhoto
                    {
                        Id = original.Id,
                        FilePath = original.FilePath,
                        FileName = System.IO.Path.GetFileName(original.FilePath),
                        Extension = System.IO.Path.GetExtension(original.FilePath),
                        FileSize = original.FileSize,
                        ModifiedDateUtc = DateTime.UtcNow,
                        FolderId = 0
                    }
                };

                foreach (var file in files.Skip(1))
                {
                    group.Duplicates.Add(new DuplicatePhoto
                    {
                        Id = file.Id,
                        FilePath = file.FilePath,
                        FileName = System.IO.Path.GetFileName(file.FilePath),
                        FileSize = file.FileSize
                    });
                }

                return group;
            })
            .OrderByDescending(g => g.DuplicateCount)
            .ToList();

        lock (_progressLock)
        {
            _progress.DuplicateGroupsFound = groups.Count;
            _progress.DuplicatesFound = groups.Sum(g => g.DuplicateCount);
            _progress.ReclaimableBytes = groups.Sum(g => g.PotentialSavings);
            FireProgressChanged();
        }

        return groups;
    }

    private async Task SaveGroupsToDatabaseAsync(
        List<DuplicateGroup> groups,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Mark original
                if (group.OriginalPhoto is not null)
                {
                    var original = await context.Photos.FindAsync(
                        new object[] { group.OriginalPhoto.Id }, cancellationToken);

                    if (original is not null)
                    {
                        original.DuplicateGroupId = group.GroupId;
                        original.ContentHash = group.ContentHash;
                        original.HashCalculatedDate = DateTime.UtcNow;
                    }
                }

                // Mark duplicates
                foreach (var duplicate in group.Duplicates)
                {
                    var photo = await context.Photos.FindAsync(
                        new object[] { duplicate.Id }, cancellationToken);

                    if (photo is not null)
                    {
                        photo.DuplicateGroupId = group.GroupId;
                        photo.ContentHash = group.ContentHash;
                        photo.HashCalculatedDate = DateTime.UtcNow;
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Saved {GroupCount} duplicate groups to database", groups.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save duplicate groups to database");
        }
    }

    private void UpdatePhase(DuplicateDetectionPhase phase)
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
