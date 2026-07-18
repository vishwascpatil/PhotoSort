using System.Collections.Concurrent;

namespace PhotoSort.Services.Memories;

public sealed class MemoryEventBus : IMemoryEventBus, IDisposable
{
    private readonly ConcurrentDictionary<MemoryEventType, List<Func<MemoryEvent, CancellationToken, Task>>> _handlers = new();
    private readonly ConcurrentQueue<MemoryEvent> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public MemoryEventBus()
    {
        _ = ProcessAsync();
    }

    public void Publish(MemoryEvent @event)
    {
        _pending.Enqueue(@event);
    }

    public IDisposable Subscribe(MemoryEventType type, Func<MemoryEvent, CancellationToken, Task> handler)
    {
        var handlers = _handlers.GetOrAdd(type, _ => []);
        lock (handlers)
            handlers.Add(handler);

        return new Subscription(() =>
        {
            lock (handlers)
                handlers.Remove(handler);
        });
    }

    private async Task ProcessAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            while (_pending.TryDequeue(out var @event))
            {
                if (_handlers.TryGetValue(@event.Type, out var handlers))
                {
                    List<Func<MemoryEvent, CancellationToken, Task>> snapshot;
                    lock (handlers)
                        snapshot = [.. handlers];

                    foreach (var handler in snapshot)
                    {
                        try { await handler(@event, _cts.Token); }
                        catch { /* swallow handler errors */ }
                    }
                }
            }
            await Task.Delay(100, _cts.Token);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _unsubscribe();
        }
    }
}
