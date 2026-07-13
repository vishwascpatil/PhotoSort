namespace PhotoSort.Models;

public sealed class ClassificationSignal
{
    public required string SignalType { get; init; }

    public required string SignalValue { get; init; }

    public double Confidence { get; init; }

    public override string ToString() => $"{SignalType}: {SignalValue} ({Confidence:P0})";
}
