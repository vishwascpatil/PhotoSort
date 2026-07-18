namespace PhotoSort.Services.Memories;

public interface IMemoryScheduler
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    Task EvaluateSchedulesAsync(CancellationToken ct = default);
}
