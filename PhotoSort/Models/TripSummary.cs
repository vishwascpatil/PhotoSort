namespace PhotoSort.Models;

public sealed class TripSummary
{
    public int TripId { get; set; }

    public required string Name { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int DurationDays { get; set; }

    public int PhotoCount { get; set; }

    public int VideoCount { get; set; }

    public int PlaceCount { get; set; }

    public double TotalDistanceKm { get; set; }

    public bool IsFavorite { get; set; }

    public string? CoverPhotoPath { get; set; }

    public IReadOnlyList<string> CityNames { get; set; } = [];

    public IReadOnlyList<string> CountryNames { get; set; } = [];

    public string DisplayDateRange => $"{StartDate:dd MMM yyyy} – {EndDate:dd MMM yyyy}";

    public string DisplayDuration => DurationDays == 1 ? "1 day" : $"{DurationDays} days";
}
