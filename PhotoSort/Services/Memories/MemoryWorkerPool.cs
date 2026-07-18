using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class MemoryWorkerPool : IMemoryWorkerPool, IDisposable
{
    private readonly IEnumerable<ISignalExtractor> _extractors;
    private readonly IMemoryDetector _detector;
    private readonly IMemoryScorer _scorer;
    private readonly IMemoryCache _cache;
    private readonly MemoryConfig _config;
    private readonly ILogger<MemoryWorkerPool> _logger;

    private readonly Channel<int> _signalChannel = Channel.CreateBounded<int>(5000);
    private readonly Channel<IReadOnlyList<int>> _candidateChannel = Channel.CreateBounded<IReadOnlyList<int>>(100);
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _workers = [];
    private bool _disposed;

    public MemoryWorkerPool(
        IEnumerable<ISignalExtractor> extractors,
        IMemoryDetector detector,
        IMemoryScorer scorer,
        IMemoryCache cache,
        IOptions<MemoryConfig> config,
        ILogger<MemoryWorkerPool> logger)
    {
        _extractors = extractors;
        _detector = detector;
        _scorer = scorer;
        _cache = cache;
        _config = config.Value;
        _logger = logger;

        StartWorkers();
    }

    private void StartWorkers()
    {
        var ct = _cts.Token;

        for (int i = 0; i < _config.Workers.SignalExtraction; i++)
        {
            _workers.Add(Task.Run(() => SignalWorkerLoop(ct), ct));
        }

        for (int i = 0; i < _config.Workers.CandidateGeneration; i++)
        {
            _workers.Add(Task.Run(() => CandidateWorkerLoop(ct), ct));
        }

        _logger.LogInformation("Started {Signal} signal workers, {Candidate} candidate workers",
            _config.Workers.SignalExtraction, _config.Workers.CandidateGeneration);
    }

    public async Task EnqueueSignalExtractionAsync(int photoId, CancellationToken ct = default)
    {
        await _signalChannel.Writer.WriteAsync(photoId, ct);
    }

    public async Task EnqueueCandidateGenerationAsync(IReadOnlyList<int> photoIds, CancellationToken ct = default)
    {
        await _candidateChannel.Writer.WriteAsync(photoIds, ct);
    }

    public Task EnqueueScoringAsync(MemoryCandidate candidate, CancellationToken ct = default)
    {
        return _scorer.ComputeScoreAsync(candidate, ct);
    }

    public Task EnqueueAssemblyAsync(MemoryCandidate candidate, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public async Task WaitForCompletionAsync(CancellationToken ct = default)
    {
        _signalChannel.Writer.Complete();
        _candidateChannel.Writer.Complete();
        await Task.WhenAll(_workers);
    }

    private async Task SignalWorkerLoop(CancellationToken ct)
    {
        await foreach (var photoId in _signalChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                var tasks = _extractors.Select(e => e.ExtractAsync(photoId, ct));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Signal extraction failed for photo {PhotoId}", photoId);
            }
        }
    }

    private async Task CandidateWorkerLoop(CancellationToken ct)
    {
        await foreach (var photoIds in _candidateChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                var candidates = await _detector.DetectCandidatesAsync(photoIds, ct);
                foreach (var c in candidates)
                    await _scorer.ComputeScoreAsync(c, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Candidate generation failed for batch of {Count} photos", photoIds.Count);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
