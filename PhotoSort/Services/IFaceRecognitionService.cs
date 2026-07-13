using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IFaceRecognitionService : IDisposable
{
    bool IsInitialized { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<double> ComputeSimilarityAsync(
        float[] embedding1,
        float[] embedding2);

    Task<IReadOnlyList<ClusterResult>> ClusterEmbeddingsAsync(
        IReadOnlyList<FaceEmbedding> embeddings,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClusterResult>> ClusterWithExistingPeopleAsync(
        IReadOnlyList<FaceEmbedding> newEmbeddings,
        IReadOnlyList<(int PersonId, float[] Centroid)> existingPeople,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default);
}

public sealed class ClusterResult
{
    public int ClusterId { get; init; }

    public int FaceId { get; init; }

    public int PersonId { get; init; }

    public double Similarity { get; init; }

    public bool IsNewPerson { get; init; }

    public IReadOnlyList<int> FaceIds { get; init; } = [];

    public float[] Centroid { get; init; } = [];

    public int Size { get; init; }
}
