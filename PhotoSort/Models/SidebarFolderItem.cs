using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PhotoSort.Models;

public sealed class SidebarFolderItem : INotifyPropertyChanged
{
    public int Id { get; init; }
    public required string DisplayName { get; set; }
    public required string FolderPath { get; init; }
    public int PhotoCount { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
