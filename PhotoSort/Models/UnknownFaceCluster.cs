namespace PhotoSort.Models;

public sealed class UnknownFaceCluster
{
    public int Id { get; set; }
    public string? ClusterLabel { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int PhotoCount { get; set; } = 1;
    public byte[]? CentroidEmbedding { get; set; }
    public bool IsNamed { get; set; }
    public string? Name { get; set; }
}
