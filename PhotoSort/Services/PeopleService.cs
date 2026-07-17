using System.Diagnostics;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class PeopleService : IPeopleService
{
    private readonly IFaceDetectionService _faceDetectionService;
    private readonly IFaceEmbeddingService _faceEmbeddingService;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly IFaceRepository _faceRepository;
    private readonly IFaceEmbeddingRepository _faceEmbeddingRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IPersonRepository _personRepository;
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly ILogger<PeopleService> _logger;

    private CancellationTokenSource _cts;
    private readonly ManualResetEventSlim _pauseEvent;
    private readonly object _progressLock;

    private FaceProcessingProgress _progress;
    private Task? _processingTask;
    private bool _disposed;

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v", ".3gp", ".flv",
        ".mpg", ".mpeg", ".ts", ".mts", ".m2ts"
    };

    private const int BatchSize = 50;
    private const double SimilarityThreshold = 0.6;
    private const int MinClusterSize = 2;

    public bool IsProcessing => _processingTask is { IsCompleted: false };

    public event EventHandler<FaceProcessingProgress>? ProgressChanged;
    public event EventHandler? PeopleChanged;

    public PeopleService(
        IFaceDetectionService faceDetectionService,
        IFaceEmbeddingService faceEmbeddingService,
        IFaceRecognitionService faceRecognitionService,
        IFaceRepository faceRepository,
        IFaceEmbeddingRepository faceEmbeddingRepository,
        IPhotoRepository photoRepository,
        IPersonRepository personRepository,
        IDbContextFactory<PhotoSortDbContext> contextFactory,
        ILogger<PeopleService> logger)
    {
        _faceDetectionService = faceDetectionService;
        _faceEmbeddingService = faceEmbeddingService;
        _faceRecognitionService = faceRecognitionService;
        _faceRepository = faceRepository;
        _faceEmbeddingRepository = faceEmbeddingRepository;
        _photoRepository = photoRepository;
        _personRepository = personRepository;
        _contextFactory = contextFactory;
        _logger = logger;

        _cts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true);
        _progressLock = new object();

        _progress = new FaceProcessingProgress
        {
            Phase = FaceProcessingPhase.Idle
        };
    }

    public FaceProcessingProgress GetProgress()
    {
        lock (_progressLock)
        {
            return _progress;
        }
    }

    public async Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        if (IsProcessing)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        _processingTask = Task.Run(() => RunProcessingAsync(linkedCts.Token), linkedCts.Token);

        try
        {
            await _processingTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Face processing failed");
        }
    }

    public async Task ProcessPhotoAsync(int photoId, string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await _faceDetectionService.InitializeAsync(cancellationToken);
            await _faceEmbeddingService.InitializeAsync(cancellationToken);

            var detectionResult = await _faceDetectionService.DetectFacesAsync(
                filePath, photoId, cancellationToken);

            if (!detectionResult.Success || detectionResult.FacesDetected == 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var existingFaces = await context.Faces
                .Where(f => f.PhotoId == photoId)
                .ToListAsync(cancellationToken);

            if (existingFaces.Count > 0)
                return;

            foreach (var detectedFace in detectionResult.Faces)
            {
                var alignedFaceData = await _faceDetectionService.ExtractAlignedFaceAsync(
                    filePath, detectedFace, cancellationToken);

                if (alignedFaceData is null)
                    continue;

                var face = new Face
                {
                    PhotoId = photoId,
                    BoundingBoxX = detectedFace.BoundingBoxX,
                    BoundingBoxY = detectedFace.BoundingBoxY,
                    BoundingBoxWidth = detectedFace.BoundingBoxWidth,
                    BoundingBoxHeight = detectedFace.BoundingBoxHeight,
                    Confidence = detectedFace.Confidence,
                    LandmarkX1 = detectedFace.Landmarks is { Length: >= 2 } ? detectedFace.Landmarks[0] : 0,
                    LandmarkY1 = detectedFace.Landmarks is { Length: >= 2 } ? detectedFace.Landmarks[1] : 0,
                    LandmarkX2 = detectedFace.Landmarks is { Length: >= 4 } ? detectedFace.Landmarks[2] : 0,
                    LandmarkY2 = detectedFace.Landmarks is { Length: >= 4 } ? detectedFace.Landmarks[3] : 0,
                    LandmarkX3 = detectedFace.Landmarks is { Length: >= 6 } ? detectedFace.Landmarks[4] : 0,
                    LandmarkY3 = detectedFace.Landmarks is { Length: >= 6 } ? detectedFace.Landmarks[5] : 0,
                    LandmarkX4 = detectedFace.Landmarks is { Length: >= 8 } ? detectedFace.Landmarks[6] : 0,
                    LandmarkY4 = detectedFace.Landmarks is { Length: >= 8 } ? detectedFace.Landmarks[7] : 0,
                    LandmarkX5 = detectedFace.Landmarks is { Length: >= 10 } ? detectedFace.Landmarks[8] : 0,
                    LandmarkY5 = detectedFace.Landmarks is { Length: >= 10 } ? detectedFace.Landmarks[9] : 0,
                    FaceAngle = detectedFace.FaceAngle,
                    FaceSize = detectedFace.FaceSize,
                    DetectionModelVersion = detectedFace.ModelVersion,
                    RecognitionState = RecognitionState.Detected,
                    CreatedDate = DateTime.UtcNow
                };

                context.Faces.Add(face);
                await context.SaveChangesAsync(cancellationToken);

                var faceThumbnailPath = SaveFaceThumbnail(face.Id, alignedFaceData);
                face.ThumbnailPath = faceThumbnailPath;

                var embedding = await _faceEmbeddingService.GenerateEmbeddingAsync(
                    alignedFaceData, cancellationToken: cancellationToken);

                if (embedding is not null)
                {
                    var faceEmbedding = new FaceEmbedding
                    {
                        FaceId = face.Id,
                        Embedding = embedding,
                        ModelName = "DefaultEmbedder",
                        ModelVersion = await _faceEmbeddingService.GetModelVersionAsync(),
                        EmbeddingDimension = await _faceEmbeddingService.GetEmbeddingDimensionAsync(),
                        Confidence = 1.0,
                        CreatedDate = DateTime.UtcNow
                    };

                    context.FaceEmbeddings.Add(faceEmbedding);
                    face.RecognitionState = RecognitionState.Embedded;
                }
            }

            var photo = await context.Photos.FindAsync(photoId);
            if (photo is not null)
            {
                photo.State = ProcessingState.FaceProcessed;
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process photo {PhotoId}", photoId);
        }
    }

    public async Task ProcessPendingPhotosAsync(CancellationToken cancellationToken = default)
    {
        if (IsProcessing)
            return;

        _processingTask = Task.Run(() => RunProcessingAsync(cancellationToken), cancellationToken);

        try
        {
            await _processingTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process pending photos");
        }
    }

    public void PauseProcessing()
    {
        if (IsProcessing)
        {
            _pauseEvent.Reset();
            UpdatePhase(FaceProcessingPhase.Paused);
        }
    }

    public void ResumeProcessing()
    {
        if (!IsProcessing)
            return;

        _pauseEvent.Set();

        lock (_progressLock)
        {
            if (_progress.Phase == FaceProcessingPhase.Paused)
            {
                _progress.Phase = FaceProcessingPhase.DetectingFaces;
                FireProgressChanged();
            }
        }
    }

    public void CancelProcessing()
    {
        _pauseEvent.Set();
        _cts.Cancel();
    }

    public async Task<IReadOnlyList<PersonInfo>> GetPeopleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var people = await context.People
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);

            var result = new List<PersonInfo>();
            var needsSave = false;

            foreach (var person in people)
            {
                if (string.IsNullOrEmpty(person.ThumbnailPath))
                {
                    await UpdatePersonThumbnailAsync(context, person, cancellationToken);
                    needsSave = true;
                }

                var faceCount = await context.PersonFaces
                    .CountAsync(pf => pf.PersonId == person.Id && !pf.Face.IsIgnored, cancellationToken);

                var photoCount = await context.PersonFaces
                    .Where(pf => pf.PersonId == person.Id && !pf.Face.IsIgnored)
                    .Select(pf => pf.Face.PhotoId)
                    .Distinct()
                    .CountAsync(cancellationToken);

                result.Add(new PersonInfo
                {
                    PersonId = person.Id,
                    Name = person.Name,
                    FaceCount = faceCount,
                    PhotoCount = photoCount,
                    ThumbnailPath = person.ThumbnailPath,
                    LastSeenDate = person.LastSeenDate,
                    CreatedDate = person.CreatedDate
                });
            }

            if (needsSave)
                await context.SaveChangesAsync(cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get people");
            return [];
        }
    }

    public async Task<PersonInfo?> GetPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var person = await context.People
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == personId, cancellationToken);

            if (person is null)
                return null;

            var faceCount = await context.PersonFaces
                .CountAsync(pf => pf.PersonId == personId && !pf.Face.IsIgnored, cancellationToken);

            var photoCount = await context.PersonFaces
                .Where(pf => pf.PersonId == personId && !pf.Face.IsIgnored)
                .Select(pf => pf.Face.PhotoId)
                .Distinct()
                .CountAsync(cancellationToken);

            return new PersonInfo
            {
                PersonId = person.Id,
                Name = person.Name,
                FaceCount = faceCount,
                PhotoCount = photoCount,
                ThumbnailPath = person.ThumbnailPath,
                LastSeenDate = person.LastSeenDate,
                CreatedDate = person.CreatedDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get person {PersonId}", personId);
            return null;
        }
    }

    public async Task<IReadOnlyList<FaceInfo>> GetPersonFacesAsync(int personId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var faceIds = await context.PersonFaces
                .Where(pf => pf.PersonId == personId)
                .Select(pf => pf.FaceId)
                .ToListAsync(cancellationToken);

            var faces = await context.Faces
                .AsNoTracking()
                .Where(f => faceIds.Contains(f.Id))
                .Include(f => f.Photo)
                .Include(f => f.FaceEmbedding)
                .OrderByDescending(f => f.CreatedDate)
                .ToListAsync(cancellationToken);

            return faces.Select(f => new FaceInfo
            {
                FaceId = f.Id,
                PhotoId = f.PhotoId,
                FilePath = f.Photo?.FilePath,
                FileName = f.Photo?.FileName,
                ThumbnailPath = f.ThumbnailPath,
                PersonId = personId,
                PersonName = null,
                Confidence = f.Confidence,
                BoundingBoxX = f.BoundingBoxX,
                BoundingBoxY = f.BoundingBoxY,
                BoundingBoxWidth = f.BoundingBoxWidth,
                BoundingBoxHeight = f.BoundingBoxHeight,
                CreatedDate = f.CreatedDate,
                IsIgnored = f.IsIgnored,
                HasEmbedding = f.FaceEmbedding != null
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get faces for person {PersonId}", personId);
            return [];
        }
    }

    public async Task<Person> RenamePersonAsync(int personId, string newName, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var person = await context.People.FindAsync(personId);
            if (person is null)
                throw new InvalidOperationException($"Person {personId} not found");

            person.Name = newName;
            await context.SaveChangesAsync(cancellationToken);

            PeopleChanged?.Invoke(this, EventArgs.Empty);

            _logger.LogInformation("Renamed person {PersonId} to {NewName}", personId, newName);

            return person;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename person {PersonId}", personId);
            throw;
        }
    }

    public async Task<Person> MergePeopleAsync(
        int primaryPersonId,
        IReadOnlyList<int> mergePersonIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var primaryPerson = await context.People.FindAsync(primaryPersonId);
            if (primaryPerson is null)
                throw new InvalidOperationException($"Primary person {primaryPersonId} not found");

            foreach (var mergePersonId in mergePersonIds)
            {
                if (mergePersonId == primaryPersonId)
                    continue;

                var personFaces = await context.PersonFaces
                    .Where(pf => pf.PersonId == mergePersonId)
                    .ToListAsync(cancellationToken);

                foreach (var personFace in personFaces)
                {
                    var existing = await context.PersonFaces
                        .FirstOrDefaultAsync(pf => pf.PersonId == primaryPersonId && pf.FaceId == personFace.FaceId, cancellationToken);

                    if (existing is null)
                    {
                        personFace.PersonId = primaryPersonId;
                        personFace.AssignedDate = DateTime.UtcNow;
                    }
                    else
                    {
                        context.PersonFaces.Remove(personFace);
                    }
                }

                var mergePerson = await context.People.FindAsync(mergePersonId);
                if (mergePerson is not null)
                {
                    context.People.Remove(mergePerson);
                }
            }

            await RecalculatePersonStatsAsync(context, primaryPersonId, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            PeopleChanged?.Invoke(this, EventArgs.Empty);

            _logger.LogInformation(
                "Merged {Count} people into person {PrimaryPersonId}",
                mergePersonIds.Count, primaryPersonId);

            return primaryPerson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge people");
            throw;
        }
    }

    public async Task<IReadOnlyList<Person>> SplitPersonAsync(
        int personId,
        IReadOnlyList<int> faceIdsToSplit,
        string? newPersonName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var originalPerson = await context.People.FindAsync(personId);
            if (originalPerson is null)
                throw new InvalidOperationException($"Person {personId} not found");

            if (faceIdsToSplit.Count == 0)
                throw new ArgumentException("Must specify at least one face to split");

            var newPerson = new Person
            {
                Name = newPersonName ?? $"{originalPerson.Name} (Split)",
                CreatedDate = DateTime.UtcNow
            };

            context.People.Add(newPerson);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var faceId in faceIdsToSplit)
            {
                var personFace = await context.PersonFaces
                    .FirstOrDefaultAsync(pf => pf.PersonId == personId && pf.FaceId == faceId, cancellationToken);

                if (personFace is not null)
                {
                    personFace.PersonId = newPerson.Id;
                    personFace.AssignedDate = DateTime.UtcNow;
                }
            }

            await RecalculatePersonStatsAsync(context, personId, cancellationToken);
            await RecalculatePersonStatsAsync(context, newPerson.Id, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            await UpdatePersonThumbnailAsync(context, originalPerson, cancellationToken);
            await UpdatePersonThumbnailAsync(context, newPerson, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            PeopleChanged?.Invoke(this, EventArgs.Empty);

            _logger.LogInformation(
                "Split {Count} faces from person {PersonId} into new person {NewPersonId}",
                faceIdsToSplit.Count, personId, newPerson.Id);

            return new List<Person> { originalPerson, newPerson };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to split person {PersonId}", personId);
            throw;
        }
    }

    public async Task<Face?> IgnoreFaceAsync(int faceId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var face = await context.Faces.FindAsync(faceId);
            if (face is null)
                return null;

            face.IsIgnored = true;
            await context.SaveChangesAsync(cancellationToken);

            var personFaces = await context.PersonFaces
                .Where(pf => pf.FaceId == faceId)
                .ToListAsync(cancellationToken);

            foreach (var pf in personFaces)
            {
                await RecalculatePersonStatsAsync(context, pf.PersonId, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);

            PeopleChanged?.Invoke(this, EventArgs.Empty);

            return face;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ignore face {FaceId}", faceId);
            return null;
        }
    }

    public async Task<Face?> UnignoreFaceAsync(int faceId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var face = await context.Faces.FindAsync(faceId);
            if (face is null)
                return null;

            face.IsIgnored = false;
            await context.SaveChangesAsync(cancellationToken);

            var personFaces = await context.PersonFaces
                .Where(pf => pf.FaceId == faceId)
                .ToListAsync(cancellationToken);

            foreach (var pf in personFaces)
            {
                await RecalculatePersonStatsAsync(context, pf.PersonId, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);

            PeopleChanged?.Invoke(this, EventArgs.Empty);

            return face;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unignore face {FaceId}", faceId);
            return null;
        }
    }

    public async Task DeletePersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var personFaces = await context.PersonFaces
                .Where(pf => pf.PersonId == personId)
                .ToListAsync(cancellationToken);

            context.PersonFaces.RemoveRange(personFaces);

            var person = await context.People.FindAsync(personId);
            if (person is not null)
            {
                context.People.Remove(person);
            }

            await context.SaveChangesAsync(cancellationToken);

            PeopleChanged?.Invoke(this, EventArgs.Empty);

            _logger.LogInformation("Deleted person {PersonId}", personId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete person {PersonId}", personId);
            throw;
        }
    }

    public async Task DeleteFaceAsync(int faceId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var personFaces = await context.PersonFaces
                .Where(pf => pf.FaceId == faceId)
                .ToListAsync(cancellationToken);

            var affectedPersonIds = personFaces.Select(pf => pf.PersonId).Distinct().ToList();

            context.PersonFaces.RemoveRange(personFaces);

            var embedding = await context.FaceEmbeddings
                .FirstOrDefaultAsync(e => e.FaceId == faceId, cancellationToken);

            if (embedding is not null)
            {
                context.FaceEmbeddings.Remove(embedding);
            }

            var face = await context.Faces.FindAsync(faceId);
            if (face is not null)
            {
                context.Faces.Remove(face);
            }

            foreach (var personId in affectedPersonIds)
            {
                await RecalculatePersonStatsAsync(context, personId, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);

            PeopleChanged?.Invoke(this, EventArgs.Empty);

            _logger.LogInformation("Deleted face {FaceId}", faceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete face {FaceId}", faceId);
            throw;
        }
    }

    public async Task ReprocessPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var personFaces = await context.PersonFaces
                .Where(pf => pf.PersonId == personId)
                .ToListAsync(cancellationToken);

            context.PersonFaces.RemoveRange(personFaces);

            var person = await context.People.FindAsync(personId);
            if (person is not null)
            {
                person.FaceCount = 0;
                person.PhotoCount = 0;
            }

            await context.SaveChangesAsync(cancellationToken);

            await ProcessPendingPhotosAsync(cancellationToken);

            PeopleChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reprocess person {PersonId}", personId);
        }
    }

    public async Task<int> GetUnprocessedPhotoCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.Photos
                .CountAsync(p => p.State < ProcessingState.FaceProcessed, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unprocessed photo count");
            return 0;
        }
    }

    public async Task<int> GetDetectedFaceCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Faces.CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get detected face count");
            return 0;
        }
    }

    public async Task<int> GetIdentifiedPeopleCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.People.CountAsync(p => p.FaceCount > 0, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get identified people count");
            return 0;
        }
    }

    public async Task<int> GetEmbeddedFaceCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.FaceEmbeddings.CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embedded face count");
            return 0;
        }
    }

    public async Task<IReadOnlyList<FaceInfo>> GetUnnamedFacesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var faces = await context.Faces
                .AsNoTracking()
                .Where(f => !f.PersonFaces.Any() && !f.IsIgnored)
                .Include(f => f.Photo)
                .OrderByDescending(f => f.CreatedDate)
                .Take(20)
                .ToListAsync(cancellationToken);

            return faces.Select(f => new FaceInfo
            {
                FaceId = f.Id,
                PhotoId = f.PhotoId,
                FilePath = f.Photo?.FilePath,
                FileName = f.Photo?.FileName,
                ThumbnailPath = f.ThumbnailPath,
                PersonId = null,
                PersonName = null,
                Confidence = f.Confidence,
                BoundingBoxX = f.BoundingBoxX,
                BoundingBoxY = f.BoundingBoxY,
                BoundingBoxWidth = f.BoundingBoxWidth,
                BoundingBoxHeight = f.BoundingBoxHeight,
                CreatedDate = f.CreatedDate,
                IsIgnored = f.IsIgnored,
                HasEmbedding = f.FaceEmbedding != null
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unnamed faces");
            return [];
        }
    }

    public async Task<Person> NameFaceAsync(int faceId, string name, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var face = await context.Faces
            .Include(f => f.PersonFaces)
            .FirstOrDefaultAsync(f => f.Id == faceId, cancellationToken);

        if (face is null)
            throw new InvalidOperationException($"Face {faceId} not found");

        var person = new Person
        {
            Name = name,
            FaceCount = 1,
            PhotoCount = 1,
            CreatedDate = DateTime.UtcNow
        };

        context.People.Add(person);
        await context.SaveChangesAsync(cancellationToken);

        var personFace = new PersonFace
        {
            PersonId = person.Id,
            FaceId = face.Id,
            AssignedDate = DateTime.UtcNow
        };

        context.PersonFaces.Add(personFace);
        await context.SaveChangesAsync(cancellationToken);

        await UpdatePersonThumbnailAsync(context, person, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        PeopleChanged?.Invoke(this, EventArgs.Empty);

        return person;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _pauseEvent.Dispose();
    }

    private async Task RunProcessingAsync(CancellationToken cancellationToken)
    {
        try
        {
            UpdatePhase(FaceProcessingPhase.DetectingFaces);

            await _faceDetectionService.InitializeAsync(cancellationToken);
            await _faceEmbeddingService.InitializeAsync(cancellationToken);
            await _faceRecognitionService.InitializeAsync(cancellationToken);

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var unprocessedPhotos = await context.Photos
                .AsNoTracking()
                .Where(p => p.State < ProcessingState.FaceProcessed
                    && !VideoExtensions.Contains(p.Extension))
                .Select(p => new PhotoBatchItem(p.Id, p.FilePath))
                .ToListAsync(cancellationToken);

            _progress.TotalPhotos = unprocessedPhotos.Count;
            FireProgressChanged();

            if (unprocessedPhotos.Count == 0)
            {
                _logger.LogInformation("No unprocessed photos found for face detection");
                UpdatePhase(FaceProcessingPhase.Completed);
                _progress.ErrorMessage = "All photos already processed. Import new photos to detect faces.";
                FireProgressChanged();
                PeopleChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < unprocessedPhotos.Count; i += BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _pauseEvent.Wait(cancellationToken);

                var batch = unprocessedPhotos.Skip(i).Take(BatchSize).ToList();

                await ProcessBatchAsync(batch, cancellationToken);

                _progress.PhotosProcessed += batch.Count;

                if (_progress.PhotosProcessed % 10 == 0 || _progress.PhotosProcessed >= unprocessedPhotos.Count)
                {
                    sw.Stop();
                    _progress.AverageProcessingTimeMs = sw.Elapsed.TotalMilliseconds / _progress.PhotosProcessed;
                    sw.Start();
                    FireProgressChanged();
                }
            }

            UpdatePhase(FaceProcessingPhase.ClusteringFaces);
            await ClusterAndAssignFacesAsync(cancellationToken);

            UpdatePhase(FaceProcessingPhase.Completed);

            _progress.Elapsed = sw.Elapsed;
            FireProgressChanged();

            PeopleChanged?.Invoke(this, EventArgs.Empty);

            _logger.LogInformation(
                "Face processing completed: {PhotosProcessed} photos, {FacesDetected} faces, {PeopleIdentified} people",
                _progress.PhotosProcessed, _progress.FacesDetected, _progress.PeopleIdentified);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Face processing was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Face processing failed");
            _progress.ErrorMessage = $"Processing failed: {ex.Message}";
            UpdatePhase(FaceProcessingPhase.Idle);
            FireProgressChanged();
        }
    }

    private sealed record PhotoBatchItem(int Id, string FilePath);

    private async Task ProcessBatchAsync(
        IReadOnlyList<PhotoBatchItem> batch,
        CancellationToken cancellationToken)
    {
        var detectionResults = new List<FaceDetectionResult>();
        var failedPhotoIds = new List<int>();

        foreach (var photo in batch)
        {
            var result = await _faceDetectionService.DetectFacesAsync(
                photo.FilePath, photo.Id, cancellationToken);

            if (!result.Success)
            {
                failedPhotoIds.Add(photo.Id);
                _logger.LogWarning("Face detection failed for photo {PhotoId}: {Error}",
                    photo.Id, result.ErrorMessage);
                continue;
            }

            if (result.FacesDetected > 0)
            {
                detectionResults.Add(result);
            }

            _progress.FacesDetected += result.FacesDetected;
        }

        await ProcessDetectionResultsAsync(detectionResults, cancellationToken);

        var photosWithFaces = detectionResults.Select(r => r.PhotoId).ToHashSet();
        var photosWithoutFaces = batch
            .Where(p => !photosWithFaces.Contains(p.Id) && !failedPhotoIds.Contains(p.Id))
            .ToList();
        if (photosWithoutFaces.Count > 0)
        {
            await MarkPhotosAsProcessedAsync(photosWithoutFaces.Select(p => p.Id).ToList(), cancellationToken);
        }
    }

    private async Task MarkPhotosAsProcessedAsync(List<int> photoIds, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var photoId in photoIds)
        {
            var photo = await context.Photos.FindAsync([photoId], cancellationToken);
            if (photo is not null)
            {
                photo.State = ProcessingState.FaceProcessed;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessDetectionResultsAsync(
        IReadOnlyList<FaceDetectionResult> results,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        foreach (var result in results)
        {
            foreach (var detectedFace in result.Faces)
            {
                var alignedFaceData = await _faceDetectionService.ExtractAlignedFaceAsync(
                    result.FilePath, detectedFace, cancellationToken);

                if (alignedFaceData is null)
                    continue;

                var face = new Face
                {
                    PhotoId = result.PhotoId,
                    BoundingBoxX = detectedFace.BoundingBoxX,
                    BoundingBoxY = detectedFace.BoundingBoxY,
                    BoundingBoxWidth = detectedFace.BoundingBoxWidth,
                    BoundingBoxHeight = detectedFace.BoundingBoxHeight,
                    Confidence = detectedFace.Confidence,
                    LandmarkX1 = detectedFace.Landmarks is { Length: >= 2 } ? detectedFace.Landmarks[0] : 0,
                    LandmarkY1 = detectedFace.Landmarks is { Length: >= 2 } ? detectedFace.Landmarks[1] : 0,
                    LandmarkX2 = detectedFace.Landmarks is { Length: >= 4 } ? detectedFace.Landmarks[2] : 0,
                    LandmarkY2 = detectedFace.Landmarks is { Length: >= 4 } ? detectedFace.Landmarks[3] : 0,
                    LandmarkX3 = detectedFace.Landmarks is { Length: >= 6 } ? detectedFace.Landmarks[4] : 0,
                    LandmarkY3 = detectedFace.Landmarks is { Length: >= 6 } ? detectedFace.Landmarks[5] : 0,
                    LandmarkX4 = detectedFace.Landmarks is { Length: >= 8 } ? detectedFace.Landmarks[6] : 0,
                    LandmarkY4 = detectedFace.Landmarks is { Length: >= 8 } ? detectedFace.Landmarks[7] : 0,
                    LandmarkX5 = detectedFace.Landmarks is { Length: >= 10 } ? detectedFace.Landmarks[8] : 0,
                    LandmarkY5 = detectedFace.Landmarks is { Length: >= 10 } ? detectedFace.Landmarks[9] : 0,
                    FaceAngle = detectedFace.FaceAngle,
                    FaceSize = detectedFace.FaceSize,
                    DetectionModelVersion = detectedFace.ModelVersion,
                    RecognitionState = RecognitionState.Detected,
                    CreatedDate = DateTime.UtcNow
                };

                context.Faces.Add(face);
                await context.SaveChangesAsync(cancellationToken);

                var faceThumbnailPath = SaveFaceThumbnail(face.Id, alignedFaceData);
                face.ThumbnailPath = faceThumbnailPath;

                var embedding = await _faceEmbeddingService.GenerateEmbeddingAsync(
                    alignedFaceData, cancellationToken: cancellationToken);

                if (embedding is not null)
                {
                    var faceEmbedding = new FaceEmbedding
                    {
                        FaceId = face.Id,
                        Embedding = embedding,
                        ModelName = "DefaultEmbedder",
                        ModelVersion = await _faceEmbeddingService.GetModelVersionAsync(),
                        EmbeddingDimension = await _faceEmbeddingService.GetEmbeddingDimensionAsync(),
                        Confidence = 1.0,
                        CreatedDate = DateTime.UtcNow
                    };

                    context.FaceEmbeddings.Add(faceEmbedding);
                    face.RecognitionState = RecognitionState.Embedded;
                    _progress.EmbeddingsGenerated++;
                }

                _progress.AlignmentsExtracted++;
            }

            var photo = await context.Photos.FindAsync(result.PhotoId);
            if (photo is not null)
            {
                photo.State = ProcessingState.FaceProcessed;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ClusterAndAssignFacesAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var unassignedFaces = await context.Faces
            .AsNoTracking()
            .Where(f => !f.PersonFaces.Any() && !f.IsIgnored && f.FaceEmbedding != null)
            .Include(f => f.FaceEmbedding)
            .ToListAsync(cancellationToken);

        if (unassignedFaces.Count == 0)
            return;

        var embeddings = unassignedFaces.Select(f => new FaceEmbedding
        {
            Id = f.FaceEmbedding!.Id,
            FaceId = f.Id,
            Embedding = f.FaceEmbedding.Embedding,
            Confidence = f.FaceEmbedding.Confidence
        }).ToList();

        var existingCentroids = await _faceEmbeddingRepository.GetAllPersonCentroidsAsync();

        var clusters = await _faceRecognitionService.ClusterWithExistingPeopleAsync(
            embeddings, existingCentroids, SimilarityThreshold, cancellationToken);

        var newPersonCounter = 0;

        foreach (var cluster in clusters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int personId;

            if (!cluster.IsNewPerson && cluster.PersonId > 0)
            {
                personId = cluster.PersonId;
            }
            else
            {
                newPersonCounter++;
                var person = new Person
                {
                    Name = $"Person {DateTime.UtcNow:yyyyMMddHHmmss}_{newPersonCounter}",
                    CreatedDate = DateTime.UtcNow
                };

                context.People.Add(person);
                await context.SaveChangesAsync(cancellationToken);
                personId = person.Id;
            }

            var existing = await context.PersonFaces
                .FirstOrDefaultAsync(pf => pf.PersonId == personId && pf.FaceId == cluster.FaceId, cancellationToken);

            if (existing is null)
            {
                context.PersonFaces.Add(new PersonFace
                {
                    PersonId = personId,
                    FaceId = cluster.FaceId,
                    AssignedDate = DateTime.UtcNow
                });
            }

            await RecalculatePersonStatsAsync(context, personId, cancellationToken);
            _progress.PeopleIdentified++;
            await context.SaveChangesAsync(cancellationToken);
        }

        foreach (var personId in await context.People.Select(p => p.Id).ToListAsync(cancellationToken))
        {
            var person = await context.People.FindAsync(personId);
            if (person is not null)
            {
                await UpdatePersonThumbnailAsync(context, person, cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task RecalculatePersonStatsAsync(
        PhotoSortDbContext context,
        int personId,
        CancellationToken cancellationToken)
    {
        var person = await context.People.FindAsync(personId);
        if (person is null)
            return;

        person.FaceCount = await context.PersonFaces
            .CountAsync(pf => pf.PersonId == personId && !pf.Face.IsIgnored, cancellationToken);

        person.PhotoCount = await context.PersonFaces
            .Where(pf => pf.PersonId == personId && !pf.Face.IsIgnored)
            .Select(pf => pf.Face.PhotoId)
            .Distinct()
            .CountAsync(cancellationToken);

        if (person.FaceCount == 0)
        {
            person.LastSeenDate = null;
        }
        else
        {
            var lastFace = await context.PersonFaces
                .Where(pf => pf.PersonId == personId && !pf.Face.IsIgnored)
                .OrderByDescending(pf => pf.Face.CreatedDate)
                .Select(pf => pf.Face)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastFace is not null)
            {
                var photo = await context.Photos.FindAsync(lastFace.PhotoId);
                person.LastSeenDate = photo?.DateTaken ?? lastFace.CreatedDate;
            }
        }
    }

    private async Task UpdatePersonThumbnailAsync(
        PhotoSortDbContext context,
        Person person,
        CancellationToken cancellationToken)
    {
        var personFace = await context.PersonFaces
            .Where(pf => pf.PersonId == person.Id && !pf.Face.IsIgnored)
            .Include(pf => pf.Face)
            .ThenInclude(f => f.Photo)
            .OrderByDescending(pf => pf.Face.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (personFace?.Face?.Photo?.ThumbnailSmallPath is not null)
        {
            person.ThumbnailPath = personFace.Face.Photo.ThumbnailSmallPath;
            person.ThumbnailPhotoId = personFace.Face.PhotoId;
            person.LastSeenDate = personFace.Face.Photo.DateTaken ?? personFace.Face.CreatedDate;
        }
    }

    private void UpdatePhase(FaceProcessingPhase phase)
    {
        lock (_progressLock)
        {
            _progress.Phase = phase;
            FireProgressChanged();
        }
    }

    private string SaveFaceThumbnail(int faceId, byte[] faceData)
    {
        try
        {
            var facesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhotoSort", "Faces");
            Directory.CreateDirectory(facesDir);

            var filePath = Path.Combine(facesDir, $"face_{faceId}.jpg");
            File.WriteAllBytes(filePath, faceData);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save face thumbnail for face {FaceId}", faceId);
            return string.Empty;
        }
    }

    private void FireProgressChanged()
    {
        ProgressChanged?.Invoke(this, _progress);
    }
}
