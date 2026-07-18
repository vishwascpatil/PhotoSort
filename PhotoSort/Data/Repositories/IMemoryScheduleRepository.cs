using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryScheduleRepository
{
    Task InsertAsync(MemorySchedule schedule);
    Task<IReadOnlyList<MemorySchedule>> GetDueSchedulesAsync(DateTime now);
    Task MarkShownAsync(int scheduleId);
}
