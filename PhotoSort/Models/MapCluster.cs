namespace PhotoSort.Models;

public sealed class MapCluster
{
    public double CenterLatitude { get; set; }

    public double CenterLongitude { get; set; }

    public double RadiusKm { get; set; }

    public int PhotoCount { get; set; }

    public IReadOnlyList<MapLocation> Locations { get; set; } = [];

    public string? ClusterName { get; set; }

    public MapClusterLevel Level { get; set; } = MapClusterLevel.City;
}

public enum MapClusterLevel
{
    World,
    Country,
    State,
    City,
    Local
}
