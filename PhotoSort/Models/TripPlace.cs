namespace PhotoSort.Models;

public sealed class TripPlace
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public Trip Trip { get; set; } = null!;

    public int PlaceId { get; set; }

    public Place Place { get; set; } = null!;

    public DateTime FirstVisitDate { get; set; }

    public DateTime LastVisitDate { get; set; }

    public int VisitCount { get; set; }
}
