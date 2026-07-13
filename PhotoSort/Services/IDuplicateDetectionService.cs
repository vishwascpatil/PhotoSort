using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IDuplicateDetectionService : IDisposable
{
    event EventHandler<DuplicateDetectionProgress>? ProgressChanged;
    event EventHandler<IReadOnlyList<DuplicateGroup>>? DetectionCompleted;

    Task StartDetectionAsync(CancellationToken cancellationToken = default);
    void PauseDetection();
    void ResumeDetection();
    void CancelDetection();
    DuplicateDetectionProgress GetProgress();
    IReadOnlyList<DuplicateGroup> GetResults();
    Task<IReadOnlyList<DuplicateGroup>> GetDuplicateGroupsAsync();
    Task DeleteDuplicateAsync(int photoId);
    Task MarkAsOriginalAsync(int groupId, int newOriginalPhotoId);
    bool IsRunning { get; }
}
