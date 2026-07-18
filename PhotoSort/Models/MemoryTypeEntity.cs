namespace PhotoSort.Models;

public sealed class MemoryTypeEntity
{
    public int Id { get; set; }

    public int FamilyId { get; set; }

    public MemoryTypeFamily Family { get; set; } = null!;

    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public string? Tone { get; set; }

    public int MinPhotoCount { get; set; } = 1;

    public int MaxPhotoCount { get; set; } = 500;

    public string DefaultCoverStrategy { get; set; } = "single";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public int TargetIntervalDays { get; set; } = 90;

    public string? SeasonalMonths { get; set; }

    public ICollection<Memory> Memories { get; set; } = [];
}
