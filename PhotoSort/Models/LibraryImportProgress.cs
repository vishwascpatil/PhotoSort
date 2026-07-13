namespace PhotoSort.Models;

public sealed class LibraryImportProgress
{
    public string CurrentStage = string.Empty;

    public string CurrentFile = string.Empty;

    public string CurrentFolder = string.Empty;

    public int FilesDiscovered;

    public int FilesIndexed;

    public int MetadataExtracted;

    public int ThumbnailsGenerated;

    public int TotalFailed;

    public TimeSpan Elapsed;

    public bool IsPaused;

    public double Percentage { get; set; }

    public double FilesPerSecond => Elapsed.TotalSeconds > 0
        ? FilesIndexed / Elapsed.TotalSeconds
        : 0;

    public int PendingWork => FilesDiscovered - FilesIndexed;

    public string StatusMessage => CurrentStage switch
    {
        "Discovering" => $"Discovering files... {FilesDiscovered:N0} found",
        "Indexing" => $"Indexing {FilesIndexed:N0} of {FilesDiscovered:N0}",
        "Metadata" => $"Extracting metadata... {MetadataExtracted:N0} of {FilesIndexed:N0}",
        "Thumbnails" => $"Generating thumbnails... {ThumbnailsGenerated:N0} completed",
        "Complete" => $"Complete. {FilesIndexed:N0} files indexed.",
        "Paused" => "Paused",
        _ => "Preparing..."
    };
}
