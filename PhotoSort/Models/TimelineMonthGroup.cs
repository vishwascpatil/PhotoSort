using System.Collections.ObjectModel;

namespace PhotoSort.Models;

public sealed class TimelineMonthGroup
{
    public int Year { get; init; }

    public int Month { get; init; }

    public int PhotoCount { get; set; }

    public ObservableCollection<TimelineDayGroup> Days { get; } = [];

    public string DisplayName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    public bool IsExpanded { get; set; }
}
