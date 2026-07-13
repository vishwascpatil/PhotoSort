using System.Diagnostics;

namespace PhotoSort.TestHarness.Diagnostics;

public sealed class MemoryTracker : IDisposable
{
    private readonly List<MemorySnapshot> _snapshots = new();
    private readonly System.Threading.Timer _timer;
    private readonly object _lock = new();
    private bool _disposed;

    public MemoryTracker(TimeSpan samplingInterval)
    {
        _timer = new System.Threading.Timer(_ => Sample(), null, samplingInterval, samplingInterval);
    }

    public void Sample()
    {
        if (_disposed) return;

        GC.Collect(0, GCCollectionMode.Forced, false);
        var process = Process.GetCurrentProcess();

        lock (_lock)
        {
            _snapshots.Add(new MemorySnapshot
            {
                Timestamp = DateTime.UtcNow,
                WorkingSet64 = process.WorkingSet64,
                PrivateMemorySize64 = process.PrivateMemorySize64,
                GcTotalMemory = GC.GetTotalMemory(false),
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            });
        }
    }

    public MemoryReport GetReport()
    {
        lock (_lock)
        {
            if (_snapshots.Count == 0)
                return new MemoryReport();

            var first = _snapshots.First();
            var last = _snapshots.Last();
            var peak = _snapshots.MaxBy(s => s.PrivateMemorySize64)!;

            return new MemoryReport
            {
                StartMemoryBytes = first.PrivateMemorySize64,
                EndMemoryBytes = last.PrivateMemorySize64,
                PeakMemoryBytes = peak.PrivateMemorySize64,
                DeltaBytes = last.PrivateMemorySize64 - first.PrivateMemorySize64,
                PeakGcTotalBytes = _snapshots.Max(s => s.GcTotalMemory),
                Gen0Collections = last.Gen0Collections - first.Gen0Collections,
                Gen1Collections = last.Gen1Collections - first.Gen1Collections,
                Gen2Collections = last.Gen2Collections - first.Gen2Collections,
                SnapshotCount = _snapshots.Count
            };
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
        Sample();
    }
}

public sealed class MemorySnapshot
{
    public DateTime Timestamp { get; init; }
    public long WorkingSet64 { get; init; }
    public long PrivateMemorySize64 { get; init; }
    public long GcTotalMemory { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
}

public sealed class MemoryReport
{
    public long StartMemoryBytes { get; init; }
    public long EndMemoryBytes { get; init; }
    public long PeakMemoryBytes { get; init; }
    public long DeltaBytes { get; init; }
    public long PeakGcTotalBytes { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
    public int SnapshotCount { get; init; }

    public string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    public override string ToString() =>
        $"Start: {FormatBytes(StartMemoryBytes)}, End: {FormatBytes(EndMemoryBytes)}, " +
        $"Peak: {FormatBytes(PeakMemoryBytes)}, Delta: {FormatBytes(DeltaBytes)}, " +
        $"GC: Gen0={Gen0Collections}, Gen1={Gen1Collections}, Gen2={Gen2Collections}";
}
