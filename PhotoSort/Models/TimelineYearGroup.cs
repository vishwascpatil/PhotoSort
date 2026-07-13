using System.Collections.ObjectModel;

namespace PhotoSort.Models;

public sealed class TimelineYearGroup
{
    public int Year { get; init; }

    public int PhotoCount { get; set; }

    public ObservableCollection<TimelineMonthGroup> Months { get; } = [];

    public bool IsExpanded { get; set; }
}
