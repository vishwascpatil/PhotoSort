using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class VideoThumbnailBackgroundWorker : IVideoThumbnailWorker
{
    private readonly IVideoThumbnailService _thumbnailService;
    private readonly IPhotoRepository _photoRepository;
    private readonly ILogger<VideoThumbnailBackgroundWorker> _logger;

    private readonly Channel<VideoWorkItem> _highChannel;
    private readonly Channel<VideoWorkItem> _mediumChannel;
    private readonly Channel<VideoWorkItem> _lowChannel;
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentDictionary<int, bool> _inProgress;
    private readonly Stopwatch _generationTimer;

    private int _queuedCount;
    private int _generatedCount;
    private int _failedCount;
    private int _skippedCount;
    private Task[] _workers = [];
    private bool _disposed;
    private bool _paused;
    private DateTime _lastProgressFire;

    public event EventHandler<VideoThumbnailProgress>? ProgressChanged;
    public event EventHandler<int>? ThumbnailReady;

    private static readonly int WorkerCount = Math.Max(2, Environment.ProcessorCount - 2);
    private const int CurrentThumbnailVersion = 1;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(500);

    public VideoThumbnailBackgroundWorker(
        IVideoThumbnailService thumbnailService,
        IPhotoRepository photoRepository,
        ILogger<VideoThumbnailBackgroundWorker> logger)
    {
        _thumbnailService = thumbnailService;
        _photoRepository = photoRepository;
        _logger = logger;

        _highChannel = Channel.CreateBounded<VideoWorkItem>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });
        _mediumChannel = Channel.CreateBounded<VideoWorkItem>(
            new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropOldest });
        _lowChannel = Channel.CreateBounded<VideoWorkItem>(
            new BoundedChannelOptions(50000) { FullMode = BoundedChannelFullMode.DropOldest });

        _cts = new CancellationTokenSource();
        _inProgress = new ConcurrentDictionary<int, bool>();
        _generationTimer = new Stopwatch();
        _lastProgressFire = DateTime.UtcNow;
    }

    public void Enqueue(int photoId, string filePath, DateTime sourceModifiedUtc, ThumbnailPriority priority)
    {
        if (_disposed) return;
        if (!File.Exists(filePath)) return;
        if (!_inProgress.TryAdd(photoId, true)) return;

        var item = new VideoWorkItem
        {
            PhotoId = photoId,
            FilePath = filePath,
            SourceModifiedUtc = sourceModifiedUtc,
            Priority = priority
        };

        var channel = priority switch
        {
            ThumbnailPriority.High => _highChannel,
            ThumbnailPriority.Medium => _mediumChannel,
            _ => _lowChannel
        };

        if (channel.Writer.TryWrite(item))
            Interlocked.Increment(ref _queuedCount);
        else
            _inProgress.TryRemove(photoId, out _);
    }

    public void EnqueueRange(IReadOnlyList<(int PhotoId, string FilePath, DateTime SourceModifiedUtc)> items)
    {
        foreach (var (photoId, filePath, sourceModifiedUtc) in items)
            Enqueue(photoId, filePath, sourceModifiedUtc, ThumbnailPriority.Low);
    }

    public void CancelAll()
    {
        _highChannel.Writer.TryComplete();
        _mediumChannel.Writer.TryComplete();
        _lowChannel.Writer.TryComplete();
    }

    public void Pause() => _paused = true;
    public void Resume() => _paused = false;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _workers = new Task[WorkerCount];
        for (int i = 0; i < WorkerCount; i++)
        {
            int workerIndex = i;
            _workers[i] = Task.Run(() => ProcessQueueAsync(workerIndex, linkedCts.Token), linkedCts.Token);
        }

        _generationTimer.Start();
        _logger.LogInformation("VideoThumbnailWorker started with {Count} workers", WorkerCount);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts.Cancel();

        if (_workers.Length > 0)
        {
            try { await Task.WhenAll(_workers); }
            catch (OperationCanceledException) { }
            catch (AggregateException) { }
        }

        _generationTimer.Stop();
        _logger.LogInformation(
            "VideoThumbnailWorker stopped. Generated: {Gen}, Failed: {Fail}",
            _generatedCount, _failedCount);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _highChannel.Writer.TryComplete();
        _mediumChannel.Writer.TryComplete();
        _lowChannel.Writer.TryComplete();
    }

    private async Task ProcessQueueAsync(int workerIndex, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Video worker {Index} started", workerIndex);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_paused)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                continue;
            }

            VideoWorkItem? item = null;

            try
            {
                if (await _highChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    if (_highChannel.Reader.TryRead(out item))
                        Interlocked.Decrement(ref _queuedCount);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (ChannelClosedException) { break; }

            if (item is null)
            {
                try
                {
                    if (await _mediumChannel.Reader.WaitToReadAsync(cancellationToken))
                    {
                        if (_mediumChannel.Reader.TryRead(out item))
                            Interlocked.Decrement(ref _queuedCount);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ChannelClosedException) { break; }
            }

            if (item is null)
            {
                try
                {
                    if (await _lowChannel.Reader.WaitToReadAsync(cancellationToken))
                    {
                        if (_lowChannel.Reader.TryRead(out item))
                            Interlocked.Decrement(ref _queuedCount);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ChannelClosedException) { break; }
            }

            if (item is null)
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                await ProcessItemAsync(item, cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedCount);
                _logger.LogWarning(ex, "Video worker {Index} failed for {PhotoId}", workerIndex, item.PhotoId);
            }
        }

        _logger.LogDebug("Video worker {Index} stopped", workerIndex);
    }

    private async Task ProcessItemAsync(VideoWorkItem item, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _photoRepository.GetByIdAsync(item.PhotoId);
            if (existing is null)
            {
                Interlocked.Increment(ref _skippedCount);
                _logger.LogWarning("Video {PhotoId} not found in database, skipping", item.PhotoId);
                return;
            }

            if (existing.VideoThumbnailVersion >= CurrentThumbnailVersion
                && !string.IsNullOrEmpty(existing.VideoThumbnailSmallPath)
                && File.Exists(existing.VideoThumbnailSmallPath))
            {
                Interlocked.Increment(ref _skippedCount);
                return;
            }

            var info = await _thumbnailService.GetOrGenerateAsync(
                item.PhotoId, item.FilePath, item.SourceModifiedUtc, cancellationToken);

            if (info is not null)
            {
                Interlocked.Increment(ref _generatedCount);

                existing.VideoThumbnailSmallPath = info.SmallPath;
                existing.VideoThumbnailMediumPath = info.MediumPath;
                existing.VideoThumbnailLargePath = info.LargePath;
                existing.VideoThumbnailTimestamp = info.SelectedTimestamp;
                existing.VideoThumbnailScore = info.ThumbnailScore;
                existing.VideoThumbnailVersion = info.Version;
                existing.VideoThumbnailDate = DateTime.UtcNow;

                if (existing.State < ProcessingState.ThumbnailGenerated)
                    existing.State = ProcessingState.ThumbnailGenerated;

                try
                {
                    await _photoRepository.UpdateAsync(existing);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update DB for video {PhotoId}", item.PhotoId);
                }
            }
            else
            {
                Interlocked.Increment(ref _failedCount);
                _logger.LogWarning("Thumbnail generation returned null for video {PhotoId} ({File})", item.PhotoId, Path.GetFileName(item.FilePath));
            }
        }
        finally
        {
            _inProgress.TryRemove(item.PhotoId, out _);

            try
            {
                ThumbnailReady?.Invoke(this, item.PhotoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ThumbnailReady event handler failed for video {PhotoId}", item.PhotoId);
            }

            FireProgressIfNeeded();
        }
    }

    private void FireProgressIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (now - _lastProgressFire < ProgressInterval) return;
        _lastProgressFire = now;

        var elapsed = _generationTimer.Elapsed.TotalSeconds;
        var rate = elapsed > 0 ? _generatedCount / elapsed : 0;
        var remaining = rate > 0 ? _queuedCount / rate : 0;

        ProgressChanged?.Invoke(this, new VideoThumbnailProgress
        {
            ProcessedCount = _generatedCount + _failedCount + _skippedCount,
            GeneratedCount = _generatedCount,
            FailedCount = _failedCount,
            SkippedCount = _skippedCount,
            QueueLength = _queuedCount,
            CacheSizeBytes = _thumbnailService.GetCacheSizeBytes(),
            GenerationRatePerSecond = rate,
            EstimatedTimeRemainingSeconds = remaining
        });
    }

    private sealed class VideoWorkItem
    {
        public required int PhotoId { get; init; }
        public required string FilePath { get; init; }
        public required DateTime SourceModifiedUtc { get; init; }
        public ThumbnailPriority Priority { get; init; }
    }
}
