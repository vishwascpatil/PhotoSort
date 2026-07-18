namespace PhotoSort.Services.Memories;

public enum MemoryEventType
{
    PhotoAdded,
    PhotoBatchAdded,
    SignalExtracted,
    SignalsReady,
    CandidateReady,
    MemoryScored,
    MemoryAssembled,
    MemoryPublished,
    MemoryShown,
    MemoryDismissed,
    FavoriteToggled,
    PersonIdentified,
    SchedulerTick
}

public sealed class MemoryEvent
{
    public MemoryEventType Type { get; init; }
    public Dictionary<string, object> Data { get; init; } = [];
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public interface IMemoryEventBus
{
    void Publish(MemoryEvent @event);
    IDisposable Subscribe(MemoryEventType type, Func<MemoryEvent, CancellationToken, Task> handler);
}
