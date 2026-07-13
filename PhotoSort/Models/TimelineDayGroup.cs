using System.Collections.ObjectModel;

namespace PhotoSort.Models;

public sealed class TimelineDayGroup
{
    public int Year { get; init; }

    public int Month { get; init; }

    public int Day { get; init; }

    public DateTime Date { get; init; }

    public int PhotoCount { get; set; }

    public ObservableCollection<GalleryPhoto> Photos { get; } = [];

    public string DisplayName => Date.ToString("dddd, MMMM d");

    public bool IsLoaded { get; set; }
}
