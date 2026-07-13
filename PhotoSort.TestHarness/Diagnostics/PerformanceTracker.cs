using System.Diagnostics;

namespace PhotoSort.TestHarness.Diagnostics;

public sealed class PerformanceTracker
{
    private readonly Dictionary<string, TimingEntry> _entries = new();
    private readonly object _lock = new();

    public void Start(string operationName)
    {
        lock (_lock)
        {
            if (!_entries.ContainsKey(operationName))
            {
                _entries[operationName] = new TimingEntry { Name = operationName };
            }
            _entries[operationName].Start();
        }
    }

    public void Stop(string operationName)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(operationName, out var entry))
            {
                entry.Stop();
            }
        }
    }

    public T Measure<T>(string operationName, Func<T> func)
    {
        Start(operationName);
        try
        {
            return func();
        }
        finally
        {
            Stop(operationName);
        }
    }

    public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> func)
    {
        Start(operationName);
        try
        {
            return await func();
        }
        finally
        {
            Stop(operationName);
        }
    }

    public TimingReport GetReport()
    {
        lock (_lock)
        {
            return new TimingReport
            {
                Entries = _entries.Values.Select(e => new TimingEntrySnapshot
                {
                    Name = e.Name,
                    ElapsedMs = e.Elapsed.TotalMilliseconds,
                    CallCount = e.CallCount,
                    AvgMs = e.CallCount > 0 ? e.Elapsed.TotalMilliseconds / e.CallCount : 0
                }).OrderByDescending(e => e.ElapsedMs).ToList()
            };
        }
    }

    private sealed class TimingEntry
    {
        public string Name { get; init; } = "";
        public Stopwatch Stopwatch { get; } = new();
        public int CallCount { get; private set; }
        private long _totalMs;

        public TimeSpan Elapsed => TimeSpan.FromMilliseconds(_totalMs);

        public void Start()
        {
            Stopwatch.Restart();
            CallCount++;
        }

        public void Stop()
        {
            Stopwatch.Stop();
            Interlocked.Add(ref _totalMs, Stopwatch.ElapsedMilliseconds);
        }
    }
}

public sealed class TimingReport
{
    public List<TimingEntrySnapshot> Entries { get; init; } = new();
}

public sealed class TimingEntrySnapshot
{
    public string Name { get; init; } = "";
    public double ElapsedMs { get; init; }
    public int CallCount { get; init; }
    public double AvgMs { get; init; }
}
