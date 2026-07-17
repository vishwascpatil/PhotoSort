using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class TravelInsightsService : ITravelInsightsService
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly ITripRepository _tripRepository;
    private readonly IPlaceRepository _placeRepository;
    private readonly ILogger<TravelInsightsService> _logger;

    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private TravelInsights? _cachedInsights;
    private DateTime _cacheTimestamp;
    private bool _disposed;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const double MaxGapDays = 3;
    private const double MaxProximityKm = 50;
    private const double EarthRadiusKm = 6371;

    public TravelInsightsService(
        IDbContextFactory<PhotoSortDbContext> contextFactory,
        ITripRepository tripRepository,
        IPlaceRepository placeRepository,
        ILogger<TravelInsightsService> logger)
    {
        _contextFactory = contextFactory;
        _tripRepository = tripRepository;
        _placeRepository = placeRepository;
        _logger = logger;
    }

    public async Task<TravelInsights> GetInsightsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedInsights is not null && DateTime.UtcNow - _cacheTimestamp < CacheDuration)
            return _cachedInsights;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedInsights is not null && DateTime.UtcNow - _cacheTimestamp < CacheDuration)
                return _cachedInsights;

            var sw = Stopwatch.StartNew();

            var statistics = await GetStatisticsAsync(cancellationToken);
            var years = await GetTravelYearsAsync(cancellationToken);
            var countries = await GetTopCountriesAsync(10, cancellationToken);
            var cities = await GetTopCitiesAsync(10, cancellationToken);
            var trips = await GetRecentTripsAsync(20, cancellationToken);
            var achievements = await GetAchievementsAsync(cancellationToken);
            var highlights = await GetMemoryHighlightsAsync(cancellationToken);
            var analyticsCards = await GetAnalyticsCardsAsync(cancellationToken);
            var analytics = await GetAnalyticsSummaryAsync(cancellationToken);

            var insights = new TravelInsights
            {
                Statistics = statistics,
                Years = years,
                TopCountries = countries,
                TopCities = cities,
                RecentTrips = trips,
                Achievements = achievements,
                MemoryHighlights = highlights,
                AnalyticsCards = analyticsCards,
                Analytics = analytics
            };

            _cachedInsights = insights;
            _cacheTimestamp = DateTime.UtcNow;

            sw.Stop();
            _logger.LogInformation("Travel insights loaded in {Elapsed}ms", sw.ElapsedMilliseconds);

            return insights;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<TravelStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var gpsPhotos = await context.Photos
            .AsNoTracking()
            .Where(p => p.Latitude.HasValue && p.Longitude.HasValue)
            .Select(p => new { p.Id, p.DateTaken, p.Latitude, p.Longitude, p.Extension, p.CameraModel })
            .ToListAsync(cancellationToken);

        var tripPhotos = await context.TripPhotos
            .AsNoTracking()
            .Include(tp => tp.Trip)
            .Include(tp => tp.Photo)
            .ToListAsync(cancellationToken);

        var places = await context.Places
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var trips = await context.Trips
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp"
        };

        var photosWithGps = gpsPhotos.Count;
        var videosWhileTravelling = tripPhotos.Count(tp =>
            videoExtensions.Contains(Path.GetExtension(tp.Photo?.Extension ?? "")));
        var totalWhileTravelling = tripPhotos.Count;
        var travelDays = tripPhotos
            .Where(tp => tp.DateTaken != default)
            .Select(tp => tp.DateTaken.Date)
            .Distinct()
            .Count();

        var uniqueCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueCities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var place in places)
        {
            if (place.Name.Contains(','))
            {
                var parts = place.Name.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    uniqueCities.Add(parts[0].Trim());
                if (parts.Length >= 3)
                    uniqueStates.Add(parts[1].Trim());
                if (parts.Length >= 2)
                    uniqueCountries.Add(parts[^1].Trim());
            }
            else
            {
                uniqueCities.Add(place.Name);
            }
        }

        return new TravelStatistics
        {
            CountriesVisited = Math.Max(uniqueCountries.Count, trips.Count > 0 ? 1 : 0),
            StatesVisited = Math.Max(uniqueStates.Count, trips.Count > 0 ? 1 : 0),
            CitiesVisited = Math.Max(uniqueCities.Count, trips.Count > 0 ? 1 : 0),
            PlacesVisited = places.Count,
            TripsCompleted = trips.Count,
            TotalPhotosWhileTravelling = totalWhileTravelling,
            TotalVideosWhileTravelling = videosWhileTravelling,
            GpsEnabledPhotos = photosWithGps,
            TravelDays = travelDays,
            TotalDistanceKm = trips.Sum(t => t.TotalDistanceKm),
            AverageTripDurationDays = trips.Count > 0
                ? trips.Average(t => (t.EndDate - t.StartDate).TotalDays + 1)
                : 0,
            AveragePhotosPerTrip = trips.Count > 0
                ? (double)totalWhileTravelling / trips.Count
                : 0,
            FirstTravelDate = trips.Count > 0 ? trips.Min(t => t.StartDate) : null,
            LastTravelDate = trips.Count > 0 ? trips.Max(t => t.EndDate) : null
        };
    }

    public async Task<IReadOnlyList<TravelYear>> GetTravelYearsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var trips = await context.Trips
            .AsNoTracking()
            .Include(t => t.TripPhotos).ThenInclude(tp => tp.Photo)
            .Include(t => t.TripPlaces).ThenInclude(tp => tp.Place)
            .OrderByDescending(t => t.StartDate)
            .ToListAsync(cancellationToken);

        var years = trips
            .GroupBy(t => t.StartDate.Year)
            .OrderByDescending(g => g.Key)
            .Select(g => new TravelYear
            {
                Year = g.Key,
                TripCount = g.Count(),
                CityCount = g.SelectMany(t => t.TripPlaces)
                    .Select(tp => tp.Place?.Name)
                    .Where(n => n is not null)
                    .Distinct()
                    .Count(),
                CountryCount = g.SelectMany(t => t.TripPlaces)
                    .Select(tp => tp.Place?.Name?.Split(',').LastOrDefault()?.Trim())
                    .Where(n => n is not null)
                    .Distinct()
                    .Count(),
                PhotoCount = g.SelectMany(t => t.TripPhotos).Count(),
                VideoCount = g.SelectMany(t => t.TripPhotos)
                    .Count(tp => IsVideoExtension(tp.Photo?.Extension)),
                TotalDistanceKm = g.Sum(t => t.TotalDistanceKm),
                Trips = g.Select(t => MapToTripSummary(t)).ToList()
            })
            .ToList();

        return years;
    }

    public async Task<IReadOnlyList<TravelCountry>> GetTopCountriesAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var tripPlaces = await context.TripPlaces
            .AsNoTracking()
            .Include(tp => tp.Place)
            .Include(tp => tp.Trip)
            .ToListAsync(cancellationToken);

        var countries = tripPlaces
            .Where(tp => tp.Place?.Name is not null)
            .GroupBy(tp => ExtractCountry(tp.Place!.Name))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g => new TravelCountry
            {
                CountryCode = g.Key!,
                CountryName = g.Key!,
                PhotoCount = g.Sum(tp => tp.Trip?.PhotoCount ?? 0),
                VisitCount = g.Select(tp => tp.TripId).Distinct().Count(),
                CityCount = g.Select(tp => tp.Place?.Name?.Split(',').FirstOrDefault()?.Trim())
                    .Where(n => n is not null).Distinct().Count(),
                FirstVisit = g.Min(tp => tp.Trip?.StartDate),
                LastVisit = g.Max(tp => tp.Trip?.EndDate),
                TotalDistanceKm = g.Sum(tp => tp.Trip?.TotalDistanceKm ?? 0)
            })
            .OrderByDescending(c => c.PhotoCount)
            .Take(limit)
            .ToList();

        return countries;
    }

    public async Task<IReadOnlyList<TravelCity>> GetTopCitiesAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var tripPlaces = await context.TripPlaces
            .AsNoTracking()
            .Include(tp => tp.Place)
            .Include(tp => tp.Trip)
            .ToListAsync(cancellationToken);

        var cities = tripPlaces
            .Where(tp => tp.Place?.Name is not null)
            .GroupBy(tp => ExtractCity(tp.Place!.Name))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g => new TravelCity
            {
                CityName = g.Key!,
                PhotoCount = g.Sum(tp => tp.Trip?.PhotoCount ?? 0),
                VisitCount = g.Select(tp => tp.TripId).Distinct().Count(),
                FirstVisit = g.Min(tp => tp.Trip?.StartDate),
                LastVisit = g.Max(tp => tp.Trip?.EndDate),
                Latitude = g.FirstOrDefault()?.Place?.Latitude,
                Longitude = g.FirstOrDefault()?.Place?.Longitude
            })
            .OrderByDescending(c => c.PhotoCount)
            .Take(limit)
            .ToList();

        return cities;
    }

    public async Task<IReadOnlyList<TripSummary>> GetRecentTripsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var trips = await context.Trips
            .AsNoTracking()
            .Include(t => t.TripPhotos)
            .Include(t => t.TripPlaces).ThenInclude(tp => tp.Place)
            .OrderByDescending(t => t.StartDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return trips.Select(MapToTripSummary).ToList();
    }

    public async Task<IReadOnlyList<TravelAchievement>> GetAchievementsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await GetStatisticsAsync(cancellationToken);
        var achievements = new List<TravelAchievement>();

        achievements.Add(CreateAchievement("first_trip", "✈️", "First Trip",
            "Completed your first trip", stats.TripsCompleted >= 1, 1, 1));

        achievements.Add(CreateAchievement("10_trips", "✈️", "Frequent Traveller",
            "Completed 10 trips", stats.TripsCompleted >= 10, stats.TripsCompleted, 10));

        achievements.Add(CreateAchievement("100_places", "📍", "Place Explorer",
            "Visited 100 places", stats.PlacesVisited >= 100, stats.PlacesVisited, 100));

        achievements.Add(CreateAchievement("1000_gps", "📸", "1000 GPS Photos",
            "Captured 1000 photos with GPS", stats.GpsEnabledPhotos >= 1000, stats.GpsEnabledPhotos, 1000));

        achievements.Add(CreateAchievement("10_cities", "🏙️", "City Hopper",
            "Visited 10 cities", stats.CitiesVisited >= 10, stats.CitiesVisited, 10));

        achievements.Add(CreateAchievement("5_states", "🗺️", "State Explorer",
            "Visited 5 states/regions", stats.StatesVisited >= 5, stats.StatesVisited, 5));

        achievements.Add(CreateAchievement("3_countries", "🌍", "International Traveller",
            "Visited 3 countries", stats.CountriesVisited >= 3, stats.CountriesVisited, 3));

        achievements.Add(CreateAchievement("10000km", "🛣️", "10,000 KM Club",
            "Travelled 10,000 km", stats.TotalDistanceKm >= 10000, stats.TotalDistanceKm, 10000));

        achievements.Add(CreateAchievement("longest_vacation", "🏖️", "Longest Vacation",
            "Completed a trip longer than 7 days",
            stats.AverageTripDurationDays >= 7, stats.AverageTripDurationDays, 7));

        achievements.Add(CreateAchievement("weekend_explorer", "🌅", "Weekend Explorer",
            "Completed a 2-3 day trip",
            stats.TripsCompleted >= 1, 1, 1));

        achievements.Add(CreateAchievement("photo_collector", "📷", "Photo Collector",
            "Taken 500+ photos while travelling",
            stats.TotalPhotosWhileTravelling >= 500, stats.TotalPhotosWhileTravelling, 500));

        achievements.Add(CreateAchievement("world_citizen", "🌐", "World Citizen",
            "Visited 5 countries",
            stats.CountriesVisited >= 5, stats.CountriesVisited, 5));

        return achievements;
    }

    public async Task<IReadOnlyList<MemoryHighlight>> GetMemoryHighlightsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var highlights = new List<MemoryHighlight>();

        var favorites = await context.Photos
            .AsNoTracking()
            .Where(p => p.IsFavorite && p.Latitude.HasValue)
            .OrderByDescending(p => p.DateTaken)
            .Take(5)
            .Select(p => new MemoryHighlight
            {
                Id = $"fav_{p.Id}",
                Category = "Favorite",
                Title = "Favorite Memory",
                PhotoPath = p.ThumbnailMediumPath ?? p.ThumbnailSmallPath ?? p.ThumbnailPath,
                PhotoId = p.Id,
                Date = p.DateTaken
            })
            .ToListAsync(cancellationToken);

        highlights.AddRange(favorites);

        var recentTrips = await context.Trips
            .AsNoTracking()
            .OrderByDescending(t => t.StartDate)
            .Take(3)
            .ToListAsync(cancellationToken);

        foreach (var trip in recentTrips)
        {
            highlights.Add(new MemoryHighlight
            {
                Id = $"trip_{trip.Id}",
                Category = "Trip",
                Title = trip.Name,
                Description = $"{trip.StartDate:MMM yyyy} · {(trip.EndDate - trip.StartDate).Days + 1} days",
                PhotoPath = trip.CoverPhotoPath,
                Date = trip.StartDate
            });
        }

        return highlights;
    }

    public async Task<IReadOnlyList<TravelAnalyticsCard>> GetAnalyticsCardsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await GetStatisticsAsync(cancellationToken);
        var cards = new List<TravelAnalyticsCard>();

        cards.Add(new TravelAnalyticsCard { Icon = "🌍", Title = "Most Travelled Year", Value = stats.LastTravelDate?.Year.ToString() ?? "---" });
        cards.Add(new TravelAnalyticsCard { Icon = "📅", Title = "Most Travelled Month", Value = stats.LastTravelDate?.ToString("MMMM") ?? "---" });
        cards.Add(new TravelAnalyticsCard { Icon = "⏱️", Title = "Longest Vacation", Value = $"{stats.AverageTripDurationDays:F0} days avg" });
        cards.Add(new TravelAnalyticsCard { Icon = "📸", Title = "Photos Per Trip", Value = $"{stats.AveragePhotosPerTrip:F0}" });
        cards.Add(new TravelAnalyticsCard { Icon = "🛣️", Title = "Total Distance", Value = $"{stats.TotalDistanceKm:N0} km" });
        cards.Add(new TravelAnalyticsCard { Icon = "📅", Title = "Travel Days", Value = $"{stats.TravelDays}" });

        return cards;
    }

    public async Task<TripSummary?> GetTripSummaryAsync(int tripId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var trip = await context.Trips
            .AsNoTracking()
            .Include(t => t.TripPhotos)
            .Include(t => t.TripPlaces).ThenInclude(tp => tp.Place)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        return trip is null ? null : MapToTripSummary(trip);
    }

    public async Task<Trip> RenameTripAsync(int tripId, string newName, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var trip = await context.Trips.FindAsync(tripId);
        if (trip is null) throw new InvalidOperationException($"Trip {tripId} not found");
        trip.Name = newName;
        trip.LastModifiedDate = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        InvalidateCache();
        return trip;
    }

    public async Task<Trip> SetTripFavoriteAsync(int tripId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var trip = await context.Trips.FindAsync(tripId);
        if (trip is null) throw new InvalidOperationException($"Trip {tripId} not found");
        trip.IsFavorite = isFavorite;
        trip.LastModifiedDate = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        InvalidateCache();
        return trip;
    }

    public async Task<Trip> SetTripNotesAsync(int tripId, string? notes, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var trip = await context.Trips.FindAsync(tripId);
        if (trip is null) throw new InvalidOperationException($"Trip {tripId} not found");
        trip.Notes = notes;
        trip.LastModifiedDate = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        InvalidateCache();
        return trip;
    }

    public async Task<Trip> SetTripCoverPhotoAsync(int tripId, int photoId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var trip = await context.Trips.FindAsync(tripId);
        if (trip is null) throw new InvalidOperationException($"Trip {tripId} not found");

        var photo = await context.Photos.FindAsync(photoId);
        if (photo is null) throw new InvalidOperationException($"Photo {photoId} not found");

        trip.CoverPhotoId = photoId;
        trip.CoverPhotoPath = photo.ThumbnailMediumPath ?? photo.ThumbnailSmallPath ?? photo.ThumbnailPath;
        trip.LastModifiedDate = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        InvalidateCache();
        return trip;
    }

    public async Task<int> DetectTripsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var gpsPhotos = await context.Photos
            .AsNoTracking()
            .Where(p => p.Latitude.HasValue && p.Longitude.HasValue && p.DateTaken.HasValue)
            .OrderBy(p => p.DateTaken)
            .Select(p => new { p.Id, p.DateTaken, p.Latitude, p.Longitude, p.Extension, p.ThumbnailMediumPath, p.ThumbnailSmallPath, p.ThumbnailPath })
            .ToListAsync(cancellationToken);

        if (gpsPhotos.Count == 0)
            return 0;

        var existingTripPhotoIds = await context.TripPhotos
            .AsNoTracking()
            .Select(tp => tp.PhotoId)
            .ToHashSetAsync(cancellationToken);

        var newPhotos = gpsPhotos.Where(p => !existingTripPhotoIds.Contains(p.Id)).ToList();
        if (newPhotos.Count == 0)
            return 0;

        var allPhotos = gpsPhotos.Except(newPhotos)
            .Concat(newPhotos)
            .OrderBy(p => p.DateTaken)
            .ToList();

        var existingTrips = await context.Trips
            .AsNoTracking()
            .OrderByDescending(t => t.EndDate)
            .Take(1)
            .ToListAsync(cancellationToken);

        var lastTripEnd = existingTrips.FirstOrDefault()?.EndDate;

        var tripGroups = new List<List<dynamic>>();
        var currentGroup = new List<dynamic>();

        foreach (var photo in allPhotos)
        {
            if (currentGroup.Count == 0)
            {
                currentGroup.Add(photo);
                continue;
            }

            var lastPhoto = currentGroup[^1];
            var daysDiff = (photo.DateTaken!.Value - lastPhoto.DateTaken!).TotalDays;
            var distDiff = HaversineDistance(
                lastPhoto.Latitude!.Value, lastPhoto.Longitude!.Value,
                photo.Latitude!.Value, photo.Longitude!.Value);

            if (daysDiff <= MaxGapDays && distDiff <= MaxProximityKm)
            {
                currentGroup.Add(photo);
            }
            else
            {
                if (currentGroup.Count >= 3)
                    tripGroups.Add(currentGroup);
                currentGroup = [photo];
            }
        }

        if (currentGroup.Count >= 3)
            tripGroups.Add(currentGroup);

        int tripsCreated = 0;

        foreach (var group in tripGroups)
        {
            var firstPhoto = group.First();
            var lastPhoto = group.Last();
            var startDate = firstPhoto.DateTaken!;
            var endDate = lastPhoto.DateTaken!;

            double totalDistance = 0;
            for (int i = 1; i < group.Count; i++)
            {
                totalDistance += HaversineDistance(
                    group[i - 1].Latitude!.Value, group[i - 1].Longitude!.Value,
                    group[i].Latitude!.Value, group[i].Longitude!.Value);
            }

            var placeNames = group
                .Where(p => p.Latitude.HasValue && p.Longitude.HasValue)
                .Select(p => $"{Math.Round(p.Latitude!.Value, 2)},{Math.Round(p.Longitude!.Value, 2)}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var trip = new Trip
            {
                Name = $"Trip to {startDate:MMM yyyy}",
                StartDate = startDate,
                EndDate = endDate,
                StartLatitude = firstPhoto.Latitude,
                StartLongitude = firstPhoto.Longitude,
                EndLatitude = lastPhoto.Latitude,
                EndLongitude = lastPhoto.Longitude,
                TotalDistanceKm = Math.Round(totalDistance, 1),
                PhotoCount = group.Count,
                VideoCount = group.Count(p => IsVideoExtension(p.Extension ?? "")),
                PlaceCount = placeNames.Count,
                CoverPhotoPath = firstPhoto.ThumbnailMediumPath ?? firstPhoto.ThumbnailSmallPath ?? firstPhoto.ThumbnailPath,
                CoverPhotoId = firstPhoto.Id,
                CreatedDate = DateTime.UtcNow
            };

            context.Trips.Add(trip);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var photo in group)
            {
                context.TripPhotos.Add(new TripPhoto
                {
                    TripId = trip.Id,
                    PhotoId = photo.Id,
                    DateTaken = photo.DateTaken!,
                    Latitude = photo.Latitude,
                    Longitude = photo.Longitude
                });
            }

            trip.PhotoCount = group.Count;
            trip.VideoCount = group.Count(p => IsVideoExtension(p.Extension));
            await context.SaveChangesAsync(cancellationToken);

            tripsCreated++;
        }

        InvalidateCache();
        return tripsCreated;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        InvalidateCache();
        await GetInsightsAsync(cancellationToken);
    }

    public void InvalidateCache()
    {
        _cachedInsights = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cacheLock.Dispose();
    }

    private async Task<TravelAnalyticsSummary> GetAnalyticsSummaryAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var trips = await context.Trips
            .AsNoTracking()
            .Include(t => t.TripPhotos).ThenInclude(tp => tp.Photo)
            .ToListAsync(cancellationToken);

        var analytics = new TravelAnalyticsSummary();

        if (trips.Count > 0)
        {
            var bestYear = trips.GroupBy(t => t.StartDate.Year)
                .OrderByDescending(g => g.Count()).First();
            analytics.MostTravelledYear = bestYear.Key.ToString();

            var bestMonth = trips.GroupBy(t => t.StartDate.Month)
                .OrderByDescending(g => g.Count()).First();
            analytics.MostTravelledMonth = new DateTime(2024, bestMonth.Key, 1).ToString("MMMM");

            var longest = trips.OrderByDescending(t => (t.EndDate - t.StartDate).TotalDays).First();
            analytics.LongestVacation = $"{(longest.EndDate - longest.StartDate).Days + 1} days ({longest.Name})";

            var shortest = trips.Where(t => (t.EndDate - t.StartDate).TotalDays >= 0)
                .OrderBy(t => (t.EndDate - t.StartDate).TotalDays).First();
            analytics.ShortestVacation = $"{(shortest.EndDate - shortest.StartDate).Days + 1} days ({shortest.Name})";

            analytics.AverageTripDuration = $"{trips.Average(t => (t.EndDate - t.StartDate).TotalDays + 1):F1} days";

            var cameras = trips.SelectMany(t => t.TripPhotos)
                .Select(tp => tp.Photo?.CameraModel)
                .Where(c => !string.IsNullOrEmpty(c))
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            analytics.MostActiveCamera = cameras?.Key ?? "---";
        }

        return analytics;
    }

    private static TripSummary MapToTripSummary(Trip trip)
    {
        var cityNames = trip.TripPlaces
            .Select(tp => tp.Place?.Name?.Split(',').FirstOrDefault()?.Trim())
            .Where(n => n is not null)
            .Distinct()
            .ToList() as IReadOnlyList<string> ?? [];

        var countryNames = trip.TripPlaces
            .Select(tp => tp.Place?.Name?.Split(',').LastOrDefault()?.Trim())
            .Where(n => n is not null)
            .Distinct()
            .ToList() as IReadOnlyList<string> ?? [];

        return new TripSummary
        {
            TripId = trip.Id,
            Name = trip.Name,
            StartDate = trip.StartDate,
            EndDate = trip.EndDate,
            DurationDays = (trip.EndDate - trip.StartDate).Days + 1,
            PhotoCount = trip.PhotoCount,
            VideoCount = trip.VideoCount,
            PlaceCount = trip.TripPlaces.Count,
            TotalDistanceKm = trip.TotalDistanceKm,
            IsFavorite = trip.IsFavorite,
            CoverPhotoPath = trip.CoverPhotoPath,
            CityNames = cityNames,
            CountryNames = countryNames
        };
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    private static string ExtractCountry(string placeName)
    {
        var parts = placeName.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[^1].Trim() : placeName.Trim();
    }

    private static string ExtractCity(string placeName)
    {
        var parts = placeName.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 1 ? parts[0].Trim() : placeName.Trim();
    }

    private static bool IsVideoExtension(string? extension)
    {
        return extension is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" or ".flv" or ".webm" or ".m4v" or ".3gp";
    }

    private static TravelAchievement CreateAchievement(string id, string icon, string title, string description, bool unlocked, double progress, double target)
    {
        return new TravelAchievement
        {
            Id = id,
            Icon = icon,
            Title = title,
            Description = description,
            IsUnlocked = unlocked,
            UnlockedDate = unlocked ? DateTime.UtcNow : null,
            Progress = progress,
            Target = target
        };
    }
}
