namespace PhotoSort.Models;

public sealed class Person
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? ThumbnailPath { get; set; }

    public int? ThumbnailPhotoId { get; set; }

    public int FaceCount { get; set; }

    public int PhotoCount { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? LastSeenDate { get; set; }

    public ICollection<PersonFace> PersonFaces { get; set; } = [];
}
