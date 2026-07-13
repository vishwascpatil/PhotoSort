namespace PhotoSort.Models;

public sealed class Place
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public ICollection<PhotoPlace> PhotoPlaces { get; set; } = [];
}
