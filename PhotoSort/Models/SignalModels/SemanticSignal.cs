namespace PhotoSort.Models;

public sealed class SemanticSignal
{
    public string? SceneType { get; set; }
    public double SceneConfidence { get; set; }
    public List<string> DetectedObjects { get; set; } = [];
    public List<string> Attributes { get; set; } = [];
    public bool HasPet { get; set; }
    public bool HasFood { get; set; }
    public bool HasCelebration { get; set; }
    public bool HasNature { get; set; }
}
