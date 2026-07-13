namespace PhotoSort.Models;

public sealed class TravelRoute
{
    public int TripId { get; set; }

    public string? TripName { get; set; }

    public IReadOnlyList<MapWaypoint> Waypoints { get; set; } = [];

    public double TotalDistanceKm { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int PhotoCount { get; set; }
}

public sealed class MapWaypoint
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? Name { get; set; }

    public DateTime Timestamp { get; set; }

    public int? PhotoId { get; set; }

    public int SequenceOrder { get; set; }
}
