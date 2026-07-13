namespace PhotoSort.Models;

public sealed class MapLocation
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? Name { get; set; }

    public string? CountryCode { get; set; }

    public string? CountryName { get; set; }

    public string? CityName { get; set; }

    public int PhotoCount { get; set; }

    public int VisitCount { get; set; }

    public DateTime? FirstVisit { get; set; }

    public DateTime? LastVisit { get; set; }

    public string? RepresentativePhotoPath { get; set; }

    public MapLocationType LocationType { get; set; } = MapLocationType.Place;
}

public enum MapLocationType
{
    Place,
    City,
    State,
    Country,
    Landmark,
    Custom
}
