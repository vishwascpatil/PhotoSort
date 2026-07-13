using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IFaceEmbeddingService : IDisposable
{
    bool IsInitialized { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<float[]?> GenerateEmbeddingAsync(
        byte[] alignedFaceData,
        string modelVersion = "1.0",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FaceEmbedding>> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<(int FaceId, byte[] AlignedFaceData)> faces,
        IProgress<FaceProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<int> GetEmbeddingDimensionAsync();

    Task<string> GetModelVersionAsync();
}
