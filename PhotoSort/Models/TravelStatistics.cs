namespace PhotoSort.Models;

public sealed class TravelStatistics
{
    public int CountriesVisited { get; set; }

    public int StatesVisited { get; set; }

    public int CitiesVisited { get; set; }

    public int PlacesVisited { get; set; }

    public int TripsCompleted { get; set; }

    public int TotalPhotosWhileTravelling { get; set; }

    public int TotalVideosWhileTravelling { get; set; }

    public int GpsEnabledPhotos { get; set; }

    public int TravelDays { get; set; }

    public double TotalDistanceKm { get; set; }

    public double AverageTripDurationDays { get; set; }

    public double AveragePhotosPerTrip { get; set; }

    public DateTime? FirstTravelDate { get; set; }

    public DateTime? LastTravelDate { get; set; }
}
