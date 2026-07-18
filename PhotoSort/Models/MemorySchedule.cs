namespace PhotoSort.Models;

public sealed class MemorySchedule
{
    public int Id { get; set; }
    public Guid MemoryId { get; set; }
    public DateTime ScheduleDate { get; set; }
    public string ScheduleType { get; set; } = "OneTime";
    public string? Recurrence { get; set; }
    public DateTime? LastShownAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
