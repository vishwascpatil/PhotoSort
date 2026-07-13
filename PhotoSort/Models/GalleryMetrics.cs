namespace PhotoSort.Models;

public sealed class GalleryMetrics
{
    public int TotalPhotos { get; init; }

    public int LoadedPhotos { get; init; }

    public int ViewportWidth { get; init; }

    public int ViewportHeight { get; init; }

    public double LastQueryLatencyMs { get; init; }

    public int CacheSize { get; init; }

    public string Summary =>
        $"Total: {TotalPhotos:N0} | Loaded: {LoadedPhotos:N0} | " +
        $"Viewport: {ViewportWidth}x{ViewportHeight} | " +
        $"Latency: {LastQueryLatencyMs:F1}ms | Cache: {CacheSize:N0}";
}
