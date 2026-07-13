namespace PhotoSort.Services;

public interface IFaceModelProvider : IDisposable
{
    bool IsInitialized { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    string GetDetectionModelPath();

    string GetEmbeddingModelPath();

    string GetDetectionModelVersion();

    string GetEmbeddingModelVersion();

    string GetDetectionModelName();

    string GetEmbeddingModelName();

    int GetEmbeddingDimension();

    int GetDetectionInputWidth();

    int GetDetectionInputHeight();

    int GetEmbeddingInputSize();

    float GetDetectionConfidenceThreshold();

    float GetNmsIouThreshold();

    bool IsGpuAvailable();

    Models.OnnxModelConfiguration GetConfiguration();
}
