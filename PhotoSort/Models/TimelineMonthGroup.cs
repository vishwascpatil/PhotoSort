using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PhotoSort.Models;

public sealed class TimelineMonthGroup : INotifyPropertyChanged
{
    public int Year { get; init; }

    public int Month { get; init; }

    public int PhotoCount { get; set; }

    public ObservableCollection<TimelineDayGroup> Days { get; } = [];

    public string DisplayName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }
    private bool _isExpanded;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
