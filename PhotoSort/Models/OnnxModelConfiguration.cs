namespace PhotoSort.Models;

public sealed class OnnxModelConfiguration
{
    public string ModelsDirectory { get; set; } = "Models";

    public string DetectionModelFileName { get; set; } = "face_detection.onnx";

    public string EmbeddingModelFileName { get; set; } = "face_embedding.onnx";

    public string DetectionModelName { get; set; } = "DefaultDetector";

    public string EmbeddingModelName { get; set; } = "DefaultEmbedder";

    public string DetectionModelVersion { get; set; } = "1.0";

    public string EmbeddingModelVersion { get; set; } = "1.0";

    public int EmbeddingDimension { get; set; } = 512;

    public int DetectionInputWidth { get; set; } = 640;

    public int DetectionInputHeight { get; set; } = 640;

    public int EmbeddingInputSize { get; set; } = 112;

    public float DetectionConfidenceThreshold { get; set; } = 0.5f;

    public float NmsIouThreshold { get; set; } = 0.4f;

    public int MaxBatchSize { get; set; } = 16;

    public bool UseGpu { get; set; } = true;

    public int CpuThreadCount { get; set; } = 4;

    public int SessionPoolSize { get; set; } = 2;
}

public sealed class FaceRecognitionConfiguration
{
    public double SimilarityThreshold { get; set; } = 0.6;

    public int MinClusterSize { get; set; } = 2;

    public int MaxClusterSize { get; set; } = 10000;

    public int BatchSize { get; set; } = 50;

    public int MaxConcurrentInference { get; set; } = 4;

    public bool EnableIncrementalProcessing { get; set; } = true;

    public bool EnableRetryOnFailure { get; set; } = true;

    public int MaxRetries { get; set; } = 3;

    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(30);

    public int VectorSearchNeighbors { get; set; } = 100;

    public bool UseApproximateNearestNeighbor { get; set; } = false;
}
