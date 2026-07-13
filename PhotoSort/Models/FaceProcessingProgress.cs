namespace PhotoSort.Models;

public enum FaceProcessingPhase
{
    Idle,
    DetectingFaces,
    ExtractingAlignments,
    GeneratingEmbeddings,
    ClusteringFaces,
    AssigningPeople,
    Completed,
    Paused
}

public sealed class FaceProcessingProgress
{
    public FaceProcessingPhase Phase { get; set; }

    public int TotalPhotos { get; set; }

    public int PhotosProcessed { get; set; }

    public int ProcessedPhotos { get; set; }

    public int CurrentPhotoIndex { get; set; }

    public string? CurrentPhotoName { get; set; }

    public int FacesDetected { get; set; }

    public int AlignmentsExtracted { get; set; }

    public int EmbeddingsGenerated { get; set; }

    public int PeopleIdentified { get; set; }

    public int QueueLength { get; set; }

    public TimeSpan Elapsed { get; set; }

    public TimeSpan EstimatedTimeRemaining { get; set; }

    public bool IsPaused { get; set; }

    public string? ErrorMessage { get; set; }

    public double AverageProcessingTimeMs { get; set; }

    public double AverageInferenceTimeMs { get; set; }

    public double AverageEmbeddingTimeMs { get; set; }

    public double AverageClusteringTimeMs { get; set; }

    public bool IsGpuAvailable { get; set; }

    public bool IsUsingGpu { get; set; }

    public string? CurrentModelName { get; set; }

    public string? CurrentModelVersion { get; set; }

    public int FailedPhotos { get; set; }

    public int RetryQueueSize { get; set; }

    public double ProgressPercent => TotalPhotos > 0
        ? (double)PhotosProcessed / TotalPhotos * 100
        : 0;

    public string PhaseDisplay => Phase switch
    {
        FaceProcessingPhase.Idle => ErrorMessage ?? "Ready",
        FaceProcessingPhase.DetectingFaces => "Detecting faces...",
        FaceProcessingPhase.ExtractingAlignments => "Extracting aligned faces...",
        FaceProcessingPhase.GeneratingEmbeddings => "Generating embeddings...",
        FaceProcessingPhase.ClusteringFaces => "Clustering faces...",
        FaceProcessingPhase.AssigningPeople => "Assigning people...",
        FaceProcessingPhase.Completed => "Completed",
        FaceProcessingPhase.Paused => "Paused",
        _ => "Unknown"
    };
}

public sealed class FaceProcessingMetrics
{
    public long TotalPhotosProcessed { get; set; }

    public long TotalFacesDetected { get; set; }

    public long TotalFacesEmbedded { get; set; }

    public long TotalFacesRecognized { get; set; }

    public long TotalUnknownFaces { get; set; }

    public double AverageDetectionTimeMs { get; set; }

    public double AverageEmbeddingTimeMs { get; set; }

    public double AverageClusteringTimeMs { get; set; }

    public long InferenceCount { get; set; }

    public bool GpuAvailable { get; set; }

    public bool UsingGpu { get; set; }

    public int QueueLength { get; set; }

    public TimeSpan Uptime { get; set; }

    public int PhotosProcessed { get; set; }

    public int TotalEmbeddingsGenerated { get; set; }

    public double AverageInferenceTimeMs { get; set; }

    public bool IsGpuAvailable { get; set; }

    public bool IsUsingGpu { get; set; }

    public string? CurrentModelName { get; set; }

    public string? CurrentModelVersion { get; set; }

    public int FailedPhotos { get; set; }

    public int ClustersIdentified { get; set; }

    public double ProcessingTimeMs { get; set; }
}
