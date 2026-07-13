using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IPeopleService : IDisposable
{
    bool IsProcessing { get; }

    event EventHandler<FaceProcessingProgress>? ProgressChanged;

    event EventHandler? PeopleChanged;

    FaceProcessingProgress GetProgress();

    Task StartProcessingAsync(CancellationToken cancellationToken = default);

    Task ProcessPhotoAsync(int photoId, string filePath, CancellationToken cancellationToken = default);

    Task ProcessPendingPhotosAsync(CancellationToken cancellationToken = default);

    void PauseProcessing();

    void ResumeProcessing();

    void CancelProcessing();

    Task<IReadOnlyList<PersonInfo>> GetPeopleAsync(CancellationToken cancellationToken = default);

    Task<PersonInfo?> GetPersonAsync(int personId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FaceInfo>> GetPersonFacesAsync(int personId, CancellationToken cancellationToken = default);

    Task<Person> RenamePersonAsync(int personId, string newName, CancellationToken cancellationToken = default);

    Task<Person> MergePeopleAsync(
        int primaryPersonId,
        IReadOnlyList<int> mergePersonIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Person>> SplitPersonAsync(
        int personId,
        IReadOnlyList<int> faceIdsToSplit,
        string? newPersonName = null,
        CancellationToken cancellationToken = default);

    Task<Face?> IgnoreFaceAsync(int faceId, CancellationToken cancellationToken = default);

    Task<Face?> UnignoreFaceAsync(int faceId, CancellationToken cancellationToken = default);

    Task DeletePersonAsync(int personId, CancellationToken cancellationToken = default);

    Task DeleteFaceAsync(int faceId, CancellationToken cancellationToken = default);

    Task ReprocessPersonAsync(int personId, CancellationToken cancellationToken = default);

    Task<int> GetUnprocessedPhotoCountAsync(CancellationToken cancellationToken = default);

    Task<int> GetDetectedFaceCountAsync(CancellationToken cancellationToken = default);

    Task<int> GetIdentifiedPeopleCountAsync(CancellationToken cancellationToken = default);

    Task<int> GetEmbeddedFaceCountAsync(CancellationToken cancellationToken = default);
}
