using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class MemoryTitleGenerator : IMemoryTitleGenerator
{
    public string GenerateTitle(MemoryCandidate candidate)
    {
        var type = candidate.Type;
        var activity = candidate.ActivityHint;
        var holiday = candidate.HolidayHint;
        var location = candidate.LocationHint;
        var people = candidate.PersonIds;

        return type switch
        {
            MemoryType.Day => activity ?? "On This Day",
            MemoryType.Week => activity ?? "This Week",
            MemoryType.Month => activity ?? $"{candidate.DateStart:MMMM yyyy}",
            MemoryType.Trip => $"Trip to {location ?? "Unknown"}",
            MemoryType.Person => activity ?? "Moments",
            MemoryType.Location => $"At {location ?? "Unknown Location"}",
            MemoryType.Holiday => $"{holiday ?? "Holiday"} Memories",
            MemoryType.Season => $"{GetSeasonName(candidate.DateStart)} {candidate.DateStart.Year}",
            MemoryType.Activity => activity ?? "Moments",
            MemoryType.Video => "Video Highlights",
            _ => "Your Memories"
        };
    }

    private static string GetSeasonName(DateTime date)
    {
        return date.Month switch
        {
            3 or 4 or 5 => "Spring",
            6 or 7 or 8 => "Summer",
            9 or 10 or 11 => "Autumn",
            _ => "Winter"
        };
    }
}
