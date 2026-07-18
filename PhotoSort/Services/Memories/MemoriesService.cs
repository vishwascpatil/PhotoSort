using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class MemoriesService : IMemoriesService
{
    private readonly IMemoryRepository _memoryRepo;
    private readonly IMemoryScheduleRepository _scheduleRepo;
    private readonly IMemoryCache _cache;
    private readonly IMemoryPipelineOrchestrator _pipeline;
    private readonly ILogger<MemoriesService> _logger;

    public MemoriesService(
        IMemoryRepository memoryRepo,
        IMemoryScheduleRepository scheduleRepo,
        IMemoryCache cache,
        IMemoryPipelineOrchestrator pipeline,
        ILogger<MemoriesService> logger)
    {
        _memoryRepo = memoryRepo;
        _scheduleRepo = scheduleRepo;
        _cache = cache;
        _pipeline = pipeline;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Memory>> GetMemoriesAsync(
        int count = 20, int offset = 0, CancellationToken ct = default)
    {
        return await _memoryRepo.GetActiveMemoriesAsync(count, offset);
    }

    public async Task<Memory?> GetMemoryByIdAsync(Guid memoryId)
    {
        var cached = await _cache.GetAsync(memoryId);
        if (cached is not null) return cached;

        var memory = await _memoryRepo.GetByIdAsync(memoryId);
        if (memory is not null)
            await _cache.SetAsync(memoryId, memory);
        return memory;
    }

    public async Task DismissMemoryAsync(Guid memoryId)
    {
        var memory = await _memoryRepo.GetByIdAsync(memoryId);
        if (memory is null) return;
        memory.Dismissed = true;
        await _memoryRepo.UpdateAsync(memory);
        await _cache.InvalidateAsync(memoryId);
    }

    public async Task SnoozeMemoryAsync(Guid memoryId, TimeSpan duration)
    {
        var memory = await _memoryRepo.GetByIdAsync(memoryId);
        if (memory is null) return;
        memory.SnoozedUntil = DateTime.UtcNow + duration;
        await _memoryRepo.UpdateAsync(memory);
        await _cache.InvalidateAsync(memoryId);
    }

    public async Task<IReadOnlyList<Memory>> GetMemoriesByDateAsync(
        DateTime date, CancellationToken ct = default)
    {
        return await _memoryRepo.GetMemoriesByDateAsync(date);
    }

    public async Task<int> GetUnreadCountAsync()
    {
        return await _memoryRepo.GetActiveCountAsync();
    }

    public async Task<IReadOnlyList<Memory>> GetOnThisDayAsync(CancellationToken ct = default)
    {
        return await _memoryRepo.GetOnThisDayAsync();
    }

    public async Task RunPipelineAsync(CancellationToken ct = default)
    {
        await _pipeline.RunFullPipelineAsync(ct);
    }
}
