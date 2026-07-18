namespace PhotoSort.Models;

public sealed class SocialSignal
{
    public List<int> KnownPersonIds { get; set; } = [];
    public List<int> UnknownFaceCount { get; set; } = [];
    public int TotalFaces { get; set; }
    public int SmileCount { get; set; }
    public bool IsGroupPhoto => TotalFaces >= 4;
    public bool IsCouplePhoto => TotalFaces == 2 && KnownPersonIds.Count == 2;
    public double AvgFaceConfidence { get; set; }
    public double SmileScore { get; set; }
    public double Weight { get; set; }
}
