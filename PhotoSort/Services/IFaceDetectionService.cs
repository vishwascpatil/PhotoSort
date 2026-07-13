using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IFaceDetectionService : IDisposable
{
    bool IsInitialized { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<FaceDetectionResult> DetectFacesAsync(
        string filePath,
        int photoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FaceDetectionResult>> DetectFacesBatchAsync(
        IReadOnlyList<(int PhotoId, string FilePath)> photos,
        IProgress<FaceProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ExtractAlignedFaceAsync(
        string filePath,
        DetectedFace face,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ExtractAlignedFaceAsync(
        byte[] imageData,
        DetectedFace face,
        CancellationToken cancellationToken = default);
}
