using System.Text.Json;

namespace PhotoSort.Models;

public sealed class PhotoSignal
{
    public int PhotoId { get; set; }
    public string Signals { get; set; } = "{}";
    public int Version { get; set; } = 1;
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    public T? GetTyped<T>(string key) where T : class
    {
        try
        {
            var doc = JsonDocument.Parse(Signals);
            if (doc.RootElement.TryGetProperty(key, out var element))
                return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch { }
        return null;
    }

    public void SetTyped<T>(string key, T value) where T : class
    {
        try
        {
            var dict = string.IsNullOrEmpty(Signals) || Signals == "{}"
                ? new Dictionary<string, object>()
                : JsonSerializer.Deserialize<Dictionary<string, object>>(Signals) ?? [];
            dict[key] = value;
            Signals = JsonSerializer.Serialize(dict);
        }
        catch { }
    }
}
