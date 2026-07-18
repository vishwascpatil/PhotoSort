namespace PhotoSort.Models;

public sealed class PhotoInteraction
{
    public int Id { get; set; }
    public int PhotoId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
