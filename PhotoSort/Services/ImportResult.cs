namespace PhotoSort.Services;

public sealed class ImportResult
{
    public int TotalDiscovered { get; init; }

    public int TotalIndexed { get; init; }

    public int TotalMetadataExtracted { get; init; }

    public int TotalFailed { get; init; }

    public TimeSpan Duration { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool WasCancelled { get; init; }

    public bool WasPaused { get; init; }
}
