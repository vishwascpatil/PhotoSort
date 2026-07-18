namespace PhotoSort.Models;

public sealed class QualitySignal
{
    public double Sharpness { get; set; }
    public double Exposure { get; set; }
    public double Contrast { get; set; }
    public double Composition { get; set; }
    public double Overall { get; set; }
    public bool IsBlurry => Sharpness < 0.3;
}
