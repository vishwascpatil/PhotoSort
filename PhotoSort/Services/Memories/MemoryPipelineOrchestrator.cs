using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class MemoryPipelineOrchestrator : IMemoryPipelineOrchestrator
{
    private readonly IMemoryWorkerPool _workerPool;
    private readonly IMemoryDetector _detector;
    private readonly IMemoryScorer _scorer;
    private readonly IMemoryRanker _ranker;
    private readonly IMemoryCache _cache;
    private readonly IMemoryEventBus _eventBus;
    private readonly IPhotoRepository _photoRepo;
    private readonly IMemoryRepository _memoryRepo;
    private readonly ILogger<MemoryPipelineOrchestrator> _logger;

    public MemoryPipelineOrchestrator(
        IMemoryWorkerPool workerPool,
        IMemoryDetector detector,
        IMemoryScorer scorer,
        IMemoryRanker ranker,
        IMemoryCache cache,
        IMemoryEventBus eventBus,
        IPhotoRepository photoRepo,
        IMemoryRepository memoryRepo,
        ILogger<MemoryPipelineOrchestrator> logger)
    {
        _workerPool = workerPool;
        _detector = detector;
        _scorer = scorer;
        _ranker = ranker;
        _cache = cache;
        _eventBus = eventBus;
        _photoRepo = photoRepo;
        _memoryRepo = memoryRepo;
        _logger = logger;
    }

    public async Task RunFullPipelineAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting full memory pipeline...");
        _cache.Clear();

        var photoIds = await _photoRepo.GetAllIdsAsync();
        if (photoIds.Count == 0)
        {
            _logger.LogInformation("No photos to process");
            return;
        }

        _logger.LogInformation("Processing {Count} photos", photoIds.Count);

        // Fire-and-forget signal extraction in background workers
        foreach (var id in photoIds)
            await _workerPool.EnqueueSignalExtractionAsync(id, ct);

        // Process photos in small batches so memories appear progressively
        int totalSaved = 0;
        foreach (var photoBatch in photoIds.Chunk(200))
        {
            ct.ThrowIfCancellationRequested();

            // Detect candidates from this batch
            var batchCandidates = new List<MemoryCandidate>();
            foreach (var detectBatch in photoBatch.Chunk(50))
            {
                var candidates = await _detector.DetectCandidatesAsync(detectBatch, ct);
                foreach (var c in candidates)
                    await _scorer.ComputeScoreAsync(c, ct);
                batchCandidates.AddRange(candidates);
            }

            // Rank and save any memories found in this batch
            if (batchCandidates.Count > 0)
            {
                var batchMemories = await _ranker.RankAsync(batchCandidates, ct: ct);
                if (batchMemories.Count > 0)
                {
                    await _memoryRepo.BulkInsertAsync(batchMemories);
                    foreach (var m in batchMemories)
                    {
                        _eventBus.Publish(new MemoryEvent
                        {
                            Type = MemoryEventType.MemoryPublished,
                            Data = { ["MemoryId"] = m.Id }
                        });
                    }
                    totalSaved += batchMemories.Count;
                    _logger.LogInformation("Batch saved: {Count} memories (total: {Total})",
                        batchMemories.Count, totalSaved);
                }
            }
        }

        _logger.LogInformation("Full pipeline complete: {Count} memories generated", totalSaved);
    }

    public async Task RunIncrementalAsync(int photoId, CancellationToken ct = default)
    {
        await _workerPool.EnqueueSignalExtractionAsync(photoId, ct);
        await _workerPool.EnqueueCandidateGenerationAsync([photoId], ct);
        _cache.InvalidateByPhotoAsync(photoId);
    }

    public async Task RunBatchAsync(IReadOnlyList<int> photoIds, CancellationToken ct = default)
    {
        foreach (var id in photoIds)
            await _workerPool.EnqueueSignalExtractionAsync(id, ct);
        await _workerPool.EnqueueCandidateGenerationAsync(photoIds, ct);
    }

    public Task ReScoreAllAsync(CancellationToken ct = default)
    {
        _cache.Clear();
        _logger.LogInformation("Cache cleared for re-scoring");
        return Task.CompletedTask;
    }
}
