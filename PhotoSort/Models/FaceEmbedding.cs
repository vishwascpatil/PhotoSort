namespace PhotoSort.Models;

public sealed class FaceEmbedding
{
    public int Id { get; set; }

    public int FaceId { get; set; }

    public Face Face { get; set; } = null!;

    public required float[] Embedding { get; set; }

    public string ModelVersion { get; set; } = "1.0";

    public string ModelName { get; set; } = "";

    public int EmbeddingDimension { get; set; } = 512;

    public bool IsNormalized { get; set; } = true;

    public double Confidence { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
