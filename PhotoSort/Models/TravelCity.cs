namespace PhotoSort.Models;

public sealed class TravelCity
{
    public string CityName { get; set; } = string.Empty;

    public string? CountryCode { get; set; }

    public string? CountryName { get; set; }

    public int PhotoCount { get; set; }

    public int VisitCount { get; set; }

    public DateTime? FirstVisit { get; set; }

    public DateTime? LastVisit { get; set; }

    public string? RepresentativePhotoPath { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}
