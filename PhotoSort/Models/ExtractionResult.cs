namespace PhotoSort.Models;

public sealed class ExtractionResult
{
    public int TotalProcessed { get; init; }

    public int TotalSkipped { get; init; }

    public int TotalFailed { get; init; }

    public TimeSpan Duration { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool WasCancelled { get; init; }
}
