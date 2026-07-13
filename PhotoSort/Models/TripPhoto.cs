namespace PhotoSort.Models;

public sealed class TripPhoto
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public Trip Trip { get; set; } = null!;

    public int PhotoId { get; set; }

    public Photo Photo { get; set; } = null!;

    public DateTime DateTaken { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}
