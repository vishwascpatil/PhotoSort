using System.Collections.Concurrent;
using System.IO;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class PhotoIndexingService : IPhotoIndexingService
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly IFolderRepository _folderRepository;
    private readonly ILogger<PhotoIndexingService> _logger;

    private const int BatchSize = 500;
    private const int ChannelCapacity = 10_000;

    private static readonly HashSet<string> SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".heic", ".heif", ".raw", ".cr2",
        ".nef", ".arw", ".dng", ".orf", ".rw2", ".pef", ".sr2",
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp"
    ];

    private CancellationTokenSource? _cts;

    public bool IsIndexing { get; private set; }

    public PhotoIndexingService(
        IDbContextFactory<PhotoSortDbContext> contextFactory,
        IFolderRepository folderRepository,
        ILogger<PhotoIndexingService> logger)
    {
        _contextFactory = contextFactory;
        _folderRepository = folderRepository;
        _logger = logger;
    }

    public async Task<IndexingResult> IndexFolderAsync(
        string folderPath,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsIndexing)
            throw new InvalidOperationException("Indexing is already in progress.");

        IsIndexing = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var errors = new ConcurrentBag<string>();
        var progressState = new IndexingProgress();

        try
        {
            var folder = await GetOrCreateFolderAsync(folderPath);
            var existingFiles = await GetExistingFilesAsync(folder.Id);

            var channel = Channel.CreateBounded<FileMetadata>(new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

            var producerTask = ProduceFileMetadataAsync(
                folderPath, channel.Writer, progressState, progress, _cts.Token, errors);

            var consumerTask = ConsumeAndBatchAsync(
                folder.Id, channel.Reader, existingFiles, progressState, progress, _cts.Token, errors);

            await Task.WhenAll(producerTask, consumerTask);

            await UpdateFolderScanDateAsync(folder.Id);

            stopwatch.Stop();

            return new IndexingResult
            {
                TotalDiscovered = progressState.FilesDiscovered,
                TotalProcessed = progressState.FilesProcessed,
                TotalSkipped = progressState.FilesSkipped,
                TotalFailed = progressState.FilesFailed,
                Duration = stopwatch.Elapsed,
                Errors = errors.ToList(),
                WasCancelled = _cts.Token.IsCancellationRequested
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new IndexingResult
            {
                TotalDiscovered = progressState.FilesDiscovered,
                TotalProcessed = progressState.FilesProcessed,
                TotalSkipped = progressState.FilesSkipped,
                TotalFailed = progressState.FilesFailed,
                Duration = stopwatch.Elapsed,
                Errors = errors.ToList(),
                WasCancelled = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexing failed for folder: {FolderPath}", folderPath);
            stopwatch.Stop();
            return new IndexingResult
            {
                TotalDiscovered = progressState.FilesDiscovered,
                TotalProcessed = progressState.FilesProcessed,
                TotalSkipped = progressState.FilesSkipped,
                TotalFailed = progressState.FilesFailed,
                Duration = stopwatch.Elapsed,
                Errors = [.. errors, ex.Message],
                WasCancelled = false
            };
        }
        finally
        {
            IsIndexing = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public Task CancelIndexingAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task<Models.Folder> GetOrCreateFolderAsync(string folderPath)
    {
        var normalizedPath = Path.GetFullPath(folderPath);
        var existing = await _folderRepository.GetByFolderPathAsync(normalizedPath);

        if (existing is not null)
            return existing;

        var folder = new Models.Folder
        {
            FolderPath = normalizedPath,
            AddedDate = DateTime.UtcNow
        };

        await _folderRepository.AddAsync(folder);
        _logger.LogInformation("Created folder record: {FolderPath}", normalizedPath);
        return folder;
    }

    private async Task<Dictionary<string, (long Size, DateTime ModifiedUtc)>> GetExistingFilesAsync(int folderId)
    {
        await using var context = _contextFactory.CreateDbContext();

        return await context.Photos
            .Where(p => p.FolderId == folderId)
            .Select(p => new { p.FilePath, p.FileSize, p.ModifiedDateUtc })
            .ToDictionaryAsync(
                p => p.FilePath,
                p => (p.FileSize, p.ModifiedDateUtc));
    }

    private async Task ProduceFileMetadataAsync(
        string rootPath,
        ChannelWriter<FileMetadata> writer,
        IndexingProgress progressState,
        IProgress<IndexingProgress>? progress,
        CancellationToken ct,
        ConcurrentBag<string> errors)
    {
        try
        {
            var enumeratedFiles = Directory.EnumerateFiles(
                rootPath, "*.*", SearchOption.AllDirectories);

            await foreach (var filePath in AsyncEnumerable(enumeratedFiles, ct))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (!IsSupportedFile(filePath))
                        continue;

                    var fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists)
                        continue;

                    Interlocked.Increment(ref progressState.FilesDiscovered);

                    var metadata = new FileMetadata
                    {
                        FilePath = filePath,
                        FileName = fileInfo.Name,
                        Extension = fileInfo.Extension.ToLowerInvariant(),
                        FileSize = fileInfo.Length,
                        CreatedDateUtc = fileInfo.CreationTimeUtc,
                        ModifiedDateUtc = fileInfo.LastWriteTimeUtc
                    };

                    await writer.WriteAsync(metadata, ct);

                    var currentFolder = Path.GetDirectoryName(filePath);
                    if (currentFolder is not null && currentFolder != progressState.CurrentFolder)
                    {
                        progressState.CurrentFolder = currentFolder;
                        progress?.Report(progressState);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Metadata read failed: {filePath} - {ex.Message}");
                    Interlocked.Increment(ref progressState.FilesFailed);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Producer failed for root: {RootPath}", rootPath);
            errors.Add($"Scan failed: {rootPath} - {ex.Message}");
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task ConsumeAndBatchAsync(
        int folderId,
        ChannelReader<FileMetadata> reader,
        Dictionary<string, (long Size, DateTime ModifiedUtc)> existingFiles,
        IndexingProgress progressState,
        IProgress<IndexingProgress>? progress,
        CancellationToken ct,
        ConcurrentBag<string> errors)
    {
        var batch = new List<FileMetadata>(BatchSize);

        try
        {
            await foreach (var metadata in reader.ReadAllAsync(ct))
            {
                batch.Add(metadata);

                if (batch.Count >= BatchSize)
                {
                    await ProcessBatchAsync(folderId, batch, existingFiles, progressState, progress, errors);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync(folderId, batch, existingFiles, progressState, progress, errors);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consumer failed");
            errors.Add($"Consumer error: {ex.Message}");
        }
    }

    private async Task ProcessBatchAsync(
        int folderId,
        List<FileMetadata> batch,
        Dictionary<string, (long Size, DateTime ModifiedUtc)> existingFiles,
        IndexingProgress progressState,
        IProgress<IndexingProgress>? progress,
        ConcurrentBag<string> errors)
    {
        var newPhotos = new List<Photo>(batch.Count);
        var updatedPhotos = new List<Photo>(batch.Count);

        foreach (var metadata in batch)
        {
            try
            {
                if (existingFiles.TryGetValue(metadata.FilePath, out var existing))
                {
                    if (existing.Size == metadata.FileSize &&
                        existing.ModifiedUtc == metadata.ModifiedDateUtc)
                    {
                        Interlocked.Increment(ref progressState.FilesSkipped);
                        continue;
                    }

                    updatedPhotos.Add(new Photo
                    {
                        FilePath = metadata.FilePath,
                        FileName = metadata.FileName,
                        Extension = metadata.Extension,
                        FileSize = metadata.FileSize,
                        FolderId = folderId,
                        ModifiedDateUtc = metadata.ModifiedDateUtc,
                        DateTaken = null,
                        State = ProcessingState.Indexed
                    });
                }
                else
                {
                    newPhotos.Add(new Photo
                    {
                        FilePath = metadata.FilePath,
                        FileName = metadata.FileName,
                        Extension = metadata.Extension,
                        FileSize = metadata.FileSize,
                        FolderId = folderId,
                        ModifiedDateUtc = metadata.ModifiedDateUtc,
                        DateTaken = null,
                        State = ProcessingState.Indexed
                    });
                }

                Interlocked.Increment(ref progressState.FilesProcessed);
                progressState.CurrentFile = metadata.FileName;
            }
            catch (Exception ex)
            {
                errors.Add($"Process failed: {metadata.FilePath} - {ex.Message}");
                Interlocked.Increment(ref progressState.FilesFailed);
            }
        }

        if (newPhotos.Count > 0 || updatedPhotos.Count > 0)
        {
            await PersistBatchAsync(newPhotos, updatedPhotos, errors);
        }

        progress?.Report(progressState);
    }

    private async Task PersistBatchAsync(
        List<Photo> newPhotos,
        List<Photo> updatedPhotos,
        ConcurrentBag<string> errors)
    {
        try
        {
            await using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                if (newPhotos.Count > 0)
                {
                    await context.Photos.AddRangeAsync(newPhotos);
                }

                if (updatedPhotos.Count > 0)
                {
                    foreach (var photo in updatedPhotos)
                    {
                        var existing = await context.Photos
                            .FirstOrDefaultAsync(p => p.FilePath == photo.FilePath);

                        if (existing is not null)
                        {
                            existing.FileName = photo.FileName;
                            existing.Extension = photo.Extension;
                            existing.FileSize = photo.FileSize;
                            existing.ModifiedDateUtc = photo.ModifiedDateUtc;
                        }
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogDebug(
                    "Batch persisted: {NewCount} new, {UpdatedCount} updated",
                    newPhotos.Count, updatedPhotos.Count);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist batch of {Count} photos",
                newPhotos.Count + updatedPhotos.Count);
            errors.Add($"Batch persist failed: {ex.Message}");
        }
    }

    private async Task UpdateFolderScanDateAsync(int folderId)
    {
        try
        {
            var folder = await _folderRepository.GetByIdAsync(folderId);
            if (folder is not null)
            {
                folder.LastScanDate = DateTime.UtcNow;
                await _folderRepository.UpdateAsync(folder);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update scan date for folder {FolderId}", folderId);
        }
    }

    public static bool IsSupportedFileStatic(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(extension) && SupportedExtensions.Contains(extension.ToLowerInvariant());
    }

    private static bool IsSupportedFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(extension) && SupportedExtensions.Contains(extension.ToLowerInvariant());
    }

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(
        IEnumerable<T> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in source)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }
}
