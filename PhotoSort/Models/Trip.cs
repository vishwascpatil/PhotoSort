namespace PhotoSort.Models;

public sealed class Trip
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public double? StartLatitude { get; set; }

    public double? StartLongitude { get; set; }

    public double? EndLatitude { get; set; }

    public double? EndLongitude { get; set; }

    public double TotalDistanceKm { get; set; }

    public int PhotoCount { get; set; }

    public int VideoCount { get; set; }

    public int PlaceCount { get; set; }

    public bool IsFavorite { get; set; }

    public string? Notes { get; set; }

    public string? CoverPhotoPath { get; set; }

    public int? CoverPhotoId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? LastModifiedDate { get; set; }

    public ICollection<TripPhoto> TripPhotos { get; set; } = [];

    public ICollection<TripPlace> TripPlaces { get; set; } = [];
}
