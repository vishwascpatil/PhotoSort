namespace PhotoSort.Models;

public sealed class PhotoPlace
{
    public int PhotoId { get; set; }

    public Photo Photo { get; set; } = null!;

    public int PlaceId { get; set; }

    public Place Place { get; set; } = null!;
}
