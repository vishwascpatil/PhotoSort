namespace PhotoSort.Models;

public sealed class MemoryCandidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public MemoryType Type { get; set; }
    public List<int> PhotoIds { get; set; } = [];
    public List<int> PersonIds { get; set; } = [];
    public DateTime DateStart { get; set; }
    public DateTime DateEnd { get; set; }
    public string? LocationHint { get; set; }
    public string? ActivityHint { get; set; }
    public string? HolidayHint { get; set; }
    public double Score { get; set; }
    public Dictionary<string, double> SignalScores { get; set; } = [];
}
