namespace PhotoSort.Models;

public sealed class Folder
{
    public int Id { get; set; }

    public required string FolderPath { get; set; }

    public DateTime AddedDate { get; set; } = DateTime.UtcNow;

    public DateTime? LastScanDate { get; set; }

    public ICollection<Photo> Photos { get; set; } = [];
}
