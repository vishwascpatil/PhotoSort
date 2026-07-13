namespace PhotoSort.Models;

public sealed class PersonFace
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public Person Person { get; set; } = null!;

    public int FaceId { get; set; }

    public Face Face { get; set; } = null!;

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    public bool IsPrimary { get; set; }
}
