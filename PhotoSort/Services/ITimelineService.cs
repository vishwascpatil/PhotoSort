using PhotoSort.Models;

namespace PhotoSort.Services;

public interface ITimelineService
{
    Task<IReadOnlyList<TimelineYearGroup>> GetYearGroupsAsync();
    Task<IReadOnlyList<TimelineMonthGroup>> GetMonthGroupsAsync(int year);
    Task<IReadOnlyList<TimelineDayGroup>> GetDayGroupsAsync(int year, int month);
    Task<IReadOnlyList<GalleryPhoto>> GetPhotosForDayAsync(int year, int month, int day);
    Task<TimelineStats> GetTimelineStatsAsync();
}
