using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PhotoSort.Models;

public sealed class Memory : INotifyPropertyChanged
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public MemoryType Type { get; set; }

    public int? MemoryTypeEntityId { get; set; }

    public MemoryTypeEntity? MemoryTypeEntity { get; set; }

    public required string Title { get; set; }
    public string? Subtitle { get; set; }
    public string? StorySummary { get; set; }
    public int CoverPhotoId { get; set; }
    public string? CoverThumbnailPath { get; set; }
    public DateTime DateStart { get; set; }
    public DateTime DateEnd { get; set; }
    public string? LocationSummary { get; set; }
    public string? PeopleSummary { get; set; }
    public double Score { get; set; }
    public bool IsGenerated { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? LastShownAt { get; set; }
    public int ShowCount { get; set; }
    public bool Dismissed { get; set; }
    public DateTime? SnoozedUntil { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<MemoryPhoto> Photos { get; set; } = [];
    public List<int> PersonIds { get; set; } = [];

    public List<MemoryItem> Items { get; set; } = [];
    public List<MemoryScore> Scores { get; set; } = [];
    public List<MemoryFeedback> FeedbackEntries { get; set; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
