using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class ThumbnailCacheService : IThumbnailCacheService
{
    private readonly IThumbnailService _thumbnailService;
    private readonly IVideoThumbnailWorker _videoThumbnailWorker;
    private readonly IPhotoRepository _photoRepository;
    private readonly ILogger<ThumbnailCacheService> _logger;

    private readonly Channel<ThumbnailRequest> _highPriorityChannel;
    private readonly Channel<ThumbnailRequest> _mediumPriorityChannel;
    private readonly Channel<ThumbnailRequest> _lowPriorityChannel;
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentDictionary<int, bool> _inProgress;
    private readonly object _progressLock;

    private int _queuedCount;
    private int _generatedCount;
    private int _failedCount;
    private readonly Stopwatch _generationTimer;
    private DateTime _lastProgressFire;
    private Task[] _workers = [];
    private bool _disposed;

    public event EventHandler<ThumbnailProgress>? ProgressChanged;
    public event EventHandler<int>? ThumbnailReady;

    private const int WorkerCount = 2;
    private const int BatchSize = 5;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(500);

    public ThumbnailCacheService(
        IThumbnailService thumbnailService,
        IVideoThumbnailWorker videoThumbnailWorker,
        IPhotoRepository photoRepository,
        ILogger<ThumbnailCacheService> logger)
    {
        _thumbnailService = thumbnailService;
        _videoThumbnailWorker = videoThumbnailWorker;
        _photoRepository = photoRepository;
        _logger = logger;

        _highPriorityChannel = Channel.CreateBounded<ThumbnailRequest>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false
            });

        _mediumPriorityChannel = Channel.CreateBounded<ThumbnailRequest>(
            new BoundedChannelOptions(5000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false
            });

        _lowPriorityChannel = Channel.CreateBounded<ThumbnailRequest>(
            new BoundedChannelOptions(50000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false
            });

        _cts = new CancellationTokenSource();
        _inProgress = new ConcurrentDictionary<int, bool>();
        _progressLock = new object();
        _generationTimer = new Stopwatch();
        _lastProgressFire = DateTime.UtcNow;
    }

    public void Enqueue(
        int photoId,
        string filePath,
        DateTime sourceModifiedUtc,
        ThumbnailPriority priority,
        bool smallOnly = false)
    {
        if (_disposed)
            return;

        if (!File.Exists(filePath))
            return;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!IsImageExtension(extension) && !VideoThumbnailExtractor.IsVideoFile(filePath))
            return;

        if (VideoThumbnailExtractor.IsVideoFile(filePath))
        {
            _videoThumbnailWorker.Enqueue(photoId, filePath, sourceModifiedUtc, priority);
            return;
        }

        if (!_inProgress.TryAdd(photoId, true))
            return;

        var request = new ThumbnailRequest
        {
            PhotoId = photoId,
            FilePath = filePath,
            SourceModifiedUtc = sourceModifiedUtc,
            Priority = priority,
            SmallOnly = smallOnly
        };

        var channel = priority switch
        {
            ThumbnailPriority.High => _highPriorityChannel,
            ThumbnailPriority.Medium => _mediumPriorityChannel,
            _ => _lowPriorityChannel
        };

        if (channel.Writer.TryWrite(request))
        {
            Interlocked.Increment(ref _queuedCount);
        }
        else
        {
            _inProgress.TryRemove(photoId, out _);
        }
    }

    public void EnqueueRange(IReadOnlyList<ThumbnailRequest> requests)
    {
        foreach (var request in requests)
        {
            Enqueue(
                request.PhotoId,
                request.FilePath,
                request.SourceModifiedUtc,
                request.Priority,
                request.SmallOnly);
        }
    }

    public void CancelAll()
    {
        _highPriorityChannel.Writer.TryComplete();
        _mediumPriorityChannel.Writer.TryComplete();
        _lowPriorityChannel.Writer.TryComplete();
    }

    public ThumbnailProgress GetProgress()
    {
        lock (_progressLock)
        {
            var elapsed = _generationTimer.Elapsed.TotalSeconds;
            var rate = elapsed > 0 ? _generatedCount / elapsed : 0;

            return new ThumbnailProgress
            {
                QueuedCount = _queuedCount,
                GeneratedCount = _generatedCount,
                FailedCount = _failedCount,
                CacheSizeBytes = _thumbnailService.GetCacheSizeBytes(),
                GenerationRatePerSecond = rate
            };
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _cts.Token, cancellationToken);

        _workers = new Task[WorkerCount];
        for (int i = 0; i < WorkerCount; i++)
        {
            int workerIndex = i;
            _workers[i] = Task.Run(() => ProcessQueueAsync(workerIndex, linkedCts.Token), linkedCts.Token);
        }

        _generationTimer.Start();
        _logger.LogInformation("ThumbnailCacheService started with {WorkerCount} workers", WorkerCount);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts.Cancel();

        if (_workers.Length > 0)
        {
            try
            {
                await Task.WhenAll(_workers);
            }
            catch (OperationCanceledException) { }
            catch (AggregateException) { }
        }

        _generationTimer.Stop();
        _logger.LogInformation(
            "ThumbnailCacheService stopped. Generated: {Generated}, Failed: {Failed}",
            _generatedCount, _failedCount);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _highPriorityChannel.Writer.TryComplete();
        _mediumPriorityChannel.Writer.TryComplete();
        _lowPriorityChannel.Writer.TryComplete();
    }

    private async Task ProcessQueueAsync(int workerIndex, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Worker {WorkerIndex} started", workerIndex);

        while (!cancellationToken.IsCancellationRequested)
        {
            ThumbnailRequest? request = null;

            try
            {
                // Priority order: high > medium > low
                if (await _highPriorityChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    if (_highPriorityChannel.Reader.TryRead(out request))
                        Interlocked.Decrement(ref _queuedCount);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (ChannelClosedException) { break; }

            if (request is null)
            {
                try
                {
                    if (await _mediumPriorityChannel.Reader.WaitToReadAsync(cancellationToken))
                    {
                        if (_mediumPriorityChannel.Reader.TryRead(out request))
                            Interlocked.Decrement(ref _queuedCount);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ChannelClosedException) { break; }
            }

            if (request is null)
            {
                try
                {
                    if (await _lowPriorityChannel.Reader.WaitToReadAsync(cancellationToken))
                    {
                        if (_lowPriorityChannel.Reader.TryRead(out request))
                            Interlocked.Decrement(ref _queuedCount);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ChannelClosedException) { break; }
            }

            if (request is null)
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                await GenerateThumbnailAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedCount);
                _logger.LogDebug(ex, "Worker {WorkerIndex} failed processing {PhotoId}", workerIndex, request.PhotoId);
            }
        }

        _logger.LogDebug("Worker {WorkerIndex} stopped", workerIndex);
    }

    private async Task GenerateThumbnailAsync(ThumbnailRequest request, CancellationToken cancellationToken)
    {
        // Always generate small thumbnail
        var smallPath = await _thumbnailService.GetOrGenerateAsync(
            request.PhotoId, request.FilePath, request.SourceModifiedUtc,
            ThumbnailSize.Small, cancellationToken);

        if (smallPath is not null)
        {
            Interlocked.Increment(ref _generatedCount);
        }
        else
        {
            Interlocked.Increment(ref _failedCount);
        }

        // Generate medium thumbnail if not smallOnly
        if (!request.SmallOnly)
        {
            var mediumPath = await _thumbnailService.GetOrGenerateAsync(
                request.PhotoId, request.FilePath, request.SourceModifiedUtc,
                ThumbnailSize.Medium, cancellationToken);

            if (mediumPath is not null)
            {
                Interlocked.Increment(ref _generatedCount);
            }
            else
            {
                Interlocked.Increment(ref _failedCount);
            }
        }

        // Update database
        try
        {
            var photo = await _photoRepository.GetByIdAsync(request.PhotoId);
            if (photo is not null)
            {
                photo.ThumbnailSmallPath = _thumbnailService.GetThumbnailPath(request.PhotoId, ThumbnailSize.Small);
                photo.ThumbnailMediumPath = request.SmallOnly
                    ? null
                    : _thumbnailService.GetThumbnailPath(request.PhotoId, ThumbnailSize.Medium);
                photo.ThumbnailGeneratedDate = DateTime.UtcNow;

                if (photo.State < ProcessingState.ThumbnailGenerated)
                    photo.State = ProcessingState.ThumbnailGenerated;

                await _photoRepository.UpdateAsync(photo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update DB for photo {PhotoId}", request.PhotoId);
        }

        _inProgress.TryRemove(request.PhotoId, out _);

        ThumbnailReady?.Invoke(this, request.PhotoId);

        FireProgressIfNeeded();
    }

    private void FireProgressIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (now - _lastProgressFire < ProgressInterval)
            return;

        _lastProgressFire = now;
        var progress = GetProgress();
        ProgressChanged?.Invoke(this, progress);
    }

    private static bool IsImageExtension(string extension)
    {
        return extension is ".jpg" or ".jpeg" or ".png" or ".heic" or ".webp"
            or ".bmp" or ".gif" or ".tiff" or ".tif";
    }
}
