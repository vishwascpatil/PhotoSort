namespace PhotoSort.Models;

public sealed class MemoryConfig
{
    public bool Enabled { get; set; } = true;
    public int MaxMemories { get; set; } = 500;
    public int DefaultPageSize { get; set; } = 20;
    public double MinScore { get; set; } = 0.3;
    public int DiversityK { get; set; } = 50;
    public double MmrLambda { get; set; } = 0.7;
    public int AnniversaryWindowDays { get; set; } = 3;
    public int DistantAnniversaryWindowDays { get; set; } = 7;
    public int MinTripPhotos { get; set; } = 5;
    public double TripMinDistanceKm { get; set; } = 100;
    public double TripHomeRadiusKm { get; set; } = 1;

    public ScoringWeights Scoring { get; set; } = new();
    public WorkerConfig Workers { get; set; } = new();
    public SchedulerConfig Scheduler { get; set; } = new();
    public CacheConfig Cache { get; set; } = new();

    public sealed class ScoringWeights
    {
        public double Temporal { get; set; } = 0.20;
        public double Social { get; set; } = 0.25;
        public double Quality { get; set; } = 0.15;
        public double Semantic { get; set; } = 0.10;
        public double Behavioral { get; set; } = 0.10;
        public double Location { get; set; } = 0.08;
        public double Camera { get; set; } = 0.04;
        public double Burst { get; set; } = 0.03;
        public double Album { get; set; } = 0.03;
        public double Ocr { get; set; } = 0.02;
    }

    public sealed class WorkerConfig
    {
        public int SignalExtraction { get; set; } = 4;
        public int CandidateGeneration { get; set; } = 2;
        public int Scoring { get; set; } = 2;
        public int Assembly { get; set; } = 1;
    }

    public sealed class SchedulerConfig
    {
        public int IntervalMinutes { get; set; } = 30;
        public int DailyArchiveHour { get; set; } = 3;
        public int WeeklyReScoreDay { get; set; } = 6;
        public int WeeklyReScoreHour { get; set; } = 3;
    }

    public sealed class CacheConfig
    {
        public int Layer0Size { get; set; } = 200;
        public int Layer1Size { get; set; } = 1000;
        public int Layer1TtlMinutes { get; set; } = 60;
        public int CoverThumbnailSize { get; set; } = 512;
    }
}
