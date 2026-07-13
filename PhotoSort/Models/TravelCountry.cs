namespace PhotoSort.Models;

public sealed class TravelCountry
{
    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    public int PhotoCount { get; set; }

    public int VisitCount { get; set; }

    public int CityCount { get; set; }

    public DateTime? FirstVisit { get; set; }

    public DateTime? LastVisit { get; set; }

    public string? RepresentativePhotoPath { get; set; }

    public double TotalDistanceKm { get; set; }
}
