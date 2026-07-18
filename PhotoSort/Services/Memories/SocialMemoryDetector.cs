using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class SocialMemoryDetector : IMemoryDetector
{
    private readonly IFaceRepository _faceRepo;
    private readonly IPersonRepository _personRepo;
    private readonly ILogger<SocialMemoryDetector> _logger;

    public SocialMemoryDetector(
        IFaceRepository faceRepo,
        IPersonRepository personRepo,
        ILogger<SocialMemoryDetector> logger)
    {
        _faceRepo = faceRepo;
        _personRepo = personRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemoryCandidate>> DetectCandidatesAsync(
        IReadOnlyList<int> photoIds, CancellationToken ct = default)
    {
        var candidates = new List<MemoryCandidate>();
        if (photoIds.Count == 0) return candidates;

        // Group by person
        var personPhotoIds = new Dictionary<int, List<int>>();
        foreach (var photoId in photoIds)
        {
            var faces = await _faceRepo.GetByPhotoIdAsync(photoId);
            foreach (var face in faces)
            {
                var personId = face.PersonFaces.FirstOrDefault()?.PersonId;
                if (personId.HasValue)
                {
                    if (!personPhotoIds.ContainsKey(personId.Value))
                        personPhotoIds[personId.Value] = [];
                    personPhotoIds[personId.Value].Add(photoId);
                }
            }
        }

        foreach (var (personId, pIds) in personPhotoIds)
        {
            if (pIds.Count < 3) continue;

            var person = await _personRepo.GetByIdAsync(personId);
            var name = person?.Name ?? $"Person {personId}";

            candidates.Add(new MemoryCandidate
            {
                Type = MemoryType.Person,
                PhotoIds = pIds.Distinct().ToList(),
                PersonIds = [personId],
                ActivityHint = $"Moments with {name}",
                Score = pIds.Count * 0.12
            });
        }

        _logger.LogDebug("Detected {Count} social memory candidates", candidates.Count);
        return candidates;
    }
}
