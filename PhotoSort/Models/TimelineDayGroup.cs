using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PhotoSort.Models;

public sealed class TimelineDayGroup : INotifyPropertyChanged
{
    public int Year { get; init; }

    public int Month { get; init; }

    public int Day { get; init; }

    public DateTime Date { get; init; }

    public int PhotoCount { get; set; }

    public ObservableCollection<GalleryPhoto> Photos { get; } = [];

    public string DisplayName => Date.ToString("dddd, MMMM d");

    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            if (_isLoaded == value) return;
            _isLoaded = value;
            OnPropertyChanged();
        }
    }
    private bool _isLoaded;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
