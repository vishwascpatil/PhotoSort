using PhotoSort.Models;

namespace PhotoSort.Services;

public interface ISimilarPhotoService : IDisposable
{
    event EventHandler<SimilarPhotoDetectionProgress>? ProgressChanged;
    event EventHandler<IReadOnlyList<SimilarPhotoGroup>>? DetectionCompleted;

    Task StartDetectionAsync(CancellationToken cancellationToken = default);
    void PauseDetection();
    void ResumeDetection();
    void CancelDetection();
    SimilarPhotoDetectionProgress GetProgress();
    IReadOnlyList<SimilarPhotoGroup> GetResults();
    Task<IReadOnlyList<SimilarPhotoGroup>> GetSimilarGroupsAsync();
    Task DeletePhotoAsync(int photoId);
    Task IgnoreGroupAsync(int groupId);
    bool IsRunning { get; }
    int ComputeHammingDistance(ulong hash1, ulong hash2);
    ulong ComputePerceptualHash(byte[] imageData);
}
