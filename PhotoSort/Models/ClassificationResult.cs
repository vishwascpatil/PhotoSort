namespace PhotoSort.Models;

public sealed class ClassificationResult
{
    public int PhotoId { get; init; }

    public MediaCategory Category { get; init; }

    public double Confidence { get; init; }

    public IReadOnlyList<ClassificationSignal> Signals { get; init; } = [];
}
