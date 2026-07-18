using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class MemoryScheduler : IMemoryScheduler
{
    private readonly IMemoryScheduleRepository _scheduleRepo;
    private readonly IMemoryRepository _memoryRepo;
    private readonly IMemoryEventBus _eventBus;
    private readonly MemoryConfig _config;
    private readonly ILogger<MemoryScheduler> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public MemoryScheduler(
        IMemoryScheduleRepository scheduleRepo,
        IMemoryRepository memoryRepo,
        IMemoryEventBus eventBus,
        IOptions<MemoryConfig> config,
        ILogger<MemoryScheduler> logger)
    {
        _scheduleRepo = scheduleRepo;
        _memoryRepo = memoryRepo;
        _eventBus = eventBus;
        _config = config.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_loop is not null) return Task.CompletedTask;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = RunLoopAsync(_cts.Token);
        _logger.LogInformation("Memory scheduler started (interval: {Interval}min)", _config.Scheduler.IntervalMinutes);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_loop is not null)
        {
            try { await _loop; } catch (OperationCanceledException) { }
        }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    public async Task EvaluateSchedulesAsync(CancellationToken ct = default)
    {
        try
        {
            var due = await _scheduleRepo.GetDueSchedulesAsync(DateTime.UtcNow);
            foreach (var schedule in due)
            {
                var memory = await _memoryRepo.GetByIdAsync(schedule.MemoryId);
                if (memory is null || memory.Dismissed || memory.IsArchived)
                    continue;

                _eventBus.Publish(new MemoryEvent
                {
                    Type = MemoryEventType.MemoryPublished,
                    Data = { ["MemoryId"] = memory.Id }
                });

                await _scheduleRepo.MarkShownAsync(schedule.Id);
                memory.LastShownAt = DateTime.UtcNow;
                memory.ShowCount++;
                await _memoryRepo.UpdateAsync(memory);
            }

            // Archive stale
            var threshold = DateTime.UtcNow.AddDays(-90);
            await _memoryRepo.ArchiveStaleAsync(threshold);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schedule evaluation failed");
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await EvaluateSchedulesAsync(ct);
            await Task.Delay(TimeSpan.FromMinutes(_config.Scheduler.IntervalMinutes), ct);
        }
    }
}
