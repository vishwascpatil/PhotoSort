using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IOnnxFaceDetector : IDisposable
{
    bool IsInitialized { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DetectedFace>> DetectAsync(
        byte[] imageData,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DetectedFace>> DetectAsync(
        float[] pixelData,
        int width,
        int height,
        CancellationToken cancellationToken = default);

    bool IsModelLoaded();

    string GetModelVersion();

    double GetLastInferenceTimeMs();
}

public interface IOnnxFaceEmbeddingGenerator : IDisposable
{
    bool IsInitialized { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<float[]?> GenerateAsync(
        byte[] alignedFaceData,
        CancellationToken cancellationToken = default);

    Task<float[]?> GenerateAsync(
        float[] pixelData,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> GenerateBatchAsync(
        IReadOnlyList<byte[]> alignedFaces,
        CancellationToken cancellationToken = default);

    bool IsModelLoaded();

    string GetModelVersion();

    int GetEmbeddingDimension();

    double GetLastInferenceTimeMs();
}

public interface IFaceClusteringService
{
    Task<IReadOnlyList<ClusterResult>> ClusterAsync(
        IReadOnlyList<FaceEmbedding> embeddings,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClusterResult>> ClusterWithExistingAsync(
        IReadOnlyList<FaceEmbedding> newEmbeddings,
        IReadOnlyList<(int PersonId, float[] Centroid)> existingPeople,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default);

    double ComputeSimilarity(float[] embedding1, float[] embedding2);

    float[] ComputeCentroid(IReadOnlyList<float[]> embeddings);

    Task RebuildClustersAsync(
        IReadOnlyList<FaceEmbedding> allEmbeddings,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default);
}

public interface IFaceRecognitionPipeline : IDisposable
{
    bool IsInitialized { get; }

    event EventHandler<FaceProcessingProgress>? ProgressChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<FaceProcessingMetrics> ProcessPhotosAsync(
        IReadOnlyList<(int PhotoId, string FilePath)> photos,
        CancellationToken cancellationToken = default);

    Task<FaceDetectionResult> ProcessSinglePhotoAsync(
        int photoId,
        string filePath,
        CancellationToken cancellationToken = default);

    void PauseProcessing();

    void ResumeProcessing();

    void CancelProcessing();

    bool IsProcessing { get; }

    FaceProcessingMetrics GetCurrentMetrics();
}
