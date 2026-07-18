using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class SocialSignalExtractor : ISignalExtractor
{
    private readonly IPhotoRepository _photoRepo;
    private readonly IFaceRepository _faceRepo;
    private readonly IPersonRepository _personRepo;
    private readonly ILogger<SocialSignalExtractor> _logger;

    public SocialSignalExtractor(
        IPhotoRepository photoRepo,
        IFaceRepository faceRepo,
        IPersonRepository personRepo,
        ILogger<SocialSignalExtractor> logger)
    {
        _photoRepo = photoRepo;
        _faceRepo = faceRepo;
        _personRepo = personRepo;
        _logger = logger;
    }

    public bool CanExtract(string extension) => true;

    public async Task ExtractAsync(int photoId, CancellationToken ct = default)
    {
        try
        {
            var faces = await _faceRepo.GetByPhotoIdAsync(photoId);
            if (faces.Count == 0)
                return;

            var knownIds = new List<int>();
            var smileCount = 0;
            var totalConfidence = 0.0;

            foreach (var face in faces)
            {
                var personId = face.PersonFaces.FirstOrDefault()?.PersonId;
                if (personId.HasValue)
                    knownIds.Add(personId.Value);
                totalConfidence += face.Confidence;
            }

            var signal = new SocialSignal
            {
                KnownPersonIds = knownIds,
                TotalFaces = faces.Count,
                SmileCount = smileCount,
                AvgFaceConfidence = faces.Count > 0 ? totalConfidence / faces.Count : 0,
                SmileScore = faces.Count > 0 ? (double)smileCount / faces.Count : 0,
                Weight = Math.Min(1.0, faces.Count * 0.15 + smileCount * 0.1)
            };

            var photoSignal = new PhotoSignal { PhotoId = photoId };
            photoSignal.SetTyped("social", signal);
            _logger.LogDebug("Extracted social signal for photo {PhotoId}: {Faces} faces, {Known} known",
                photoId, faces.Count, knownIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract social signal for photo {PhotoId}", photoId);
        }
    }
}
