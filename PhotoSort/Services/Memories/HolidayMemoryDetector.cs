using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class HolidayMemoryDetector : IMemoryDetector
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<HolidayMemoryDetector> _logger;

    private static readonly Dictionary<string, (int Month, int Day, int Range)> Holidays = new()
    {
        ["New Year"] = (1, 1, 2),
        ["Valentine's Day"] = (2, 14, 1),
        ["Halloween"] = (10, 31, 1),
        ["Christmas"] = (12, 25, 3),
        ["Christmas Eve"] = (12, 24, 1)
    };

    public HolidayMemoryDetector(IPhotoRepository photoRepo, ILogger<HolidayMemoryDetector> logger)
    {
        _photoRepo = photoRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemoryCandidate>> DetectCandidatesAsync(
        IReadOnlyList<int> photoIds, CancellationToken ct = default)
    {
        var candidates = new List<MemoryCandidate>();
        var holidayGroups = new Dictionary<string, List<int>>();

        foreach (var id in photoIds)
        {
            var photo = await _photoRepo.GetByIdAsync(id);
            if (photo?.DateTaken is null) continue;
            var date = photo.DateTaken.Value;

            foreach (var (name, (month, day, range)) in Holidays)
            {
                if (date.Month == month && Math.Abs(date.Day - day) <= range)
                {
                    if (!holidayGroups.ContainsKey(name))
                        holidayGroups[name] = [];
                    holidayGroups[name].Add(id);
                    break;
                }
            }
        }

        foreach (var (holiday, ids) in holidayGroups)
        {
            if (ids.Count < 2) continue;

            candidates.Add(new MemoryCandidate
            {
                Type = MemoryType.Holiday,
                PhotoIds = ids,
                HolidayHint = holiday,
                Score = ids.Count * 0.1 * (holiday == "Christmas" ? 1.5 : 1.0)
            });
        }

        return candidates;
    }
}
