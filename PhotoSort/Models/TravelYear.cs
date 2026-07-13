namespace PhotoSort.Models;

public sealed class TravelYear
{
    public int Year { get; set; }

    public int TripCount { get; set; }

    public int CityCount { get; set; }

    public int CountryCount { get; set; }

    public int PhotoCount { get; set; }

    public int VideoCount { get; set; }

    public double TotalDistanceKm { get; set; }

    public IReadOnlyList<TripSummary> Trips { get; set; } = [];
}
