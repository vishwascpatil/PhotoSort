namespace PhotoSort.Models;

public enum DuplicateDetectionPhase
{
    Idle,
    CandidateIdentification,
    HashComputation,
    Grouping,
    Completed,
    Paused
}

public sealed class DuplicateDetectionProgress
{
    public DuplicateDetectionPhase Phase { get; set; }

    public int TotalFiles { get; set; }

    public int FilesProcessed { get; set; }

    public int CandidatesIdentified { get; set; }

    public int HashesComputed { get; set; }

    public int DuplicatesFound { get; set; }

    public int DuplicateGroupsFound { get; set; }

    public long ReclaimableBytes { get; set; }

    public double ProgressPercent => TotalFiles > 0
        ? (double)FilesProcessed / TotalFiles * 100
        : 0;

    public string DisplayReclaimable => FormatFileSize(ReclaimableBytes);

    public string PhaseDisplay => Phase switch
    {
        DuplicateDetectionPhase.Idle => "Ready",
        DuplicateDetectionPhase.CandidateIdentification => "Identifying candidates...",
        DuplicateDetectionPhase.HashComputation => "Computing hashes...",
        DuplicateDetectionPhase.Grouping => "Grouping duplicates...",
        DuplicateDetectionPhase.Completed => "Completed",
        DuplicateDetectionPhase.Paused => "Paused",
        _ => "Unknown"
    };

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }
}
