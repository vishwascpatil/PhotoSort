using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class MemoryCache : IMemoryCache
{
    private readonly ConcurrentDictionary<Guid, (Memory Memory, DateTime ExpiresAt)> _layer1 = new();
    private readonly MemoryConfig.CacheConfig _config;

    public MemoryCache(IOptions<MemoryConfig> config)
    {
        _config = config.Value.Cache;
    }

    public Task<Memory?> GetAsync(Guid id)
    {
        if (_layer1.TryGetValue(id, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
            return Task.FromResult<Memory?>(entry.Memory);
        return Task.FromResult<Memory?>(null);
    }

    public Task SetAsync(Guid id, Memory memory, TimeSpan? ttl = null)
    {
        var expiry = DateTime.UtcNow + (ttl ?? TimeSpan.FromMinutes(_config.Layer1TtlMinutes));
        _layer1[id] = (memory, expiry);
        EvictIfNeeded();
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(Guid id)
    {
        _layer1.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task InvalidateByPhotoAsync(int photoId)
    {
        var keys = _layer1
            .Where(kvp => kvp.Value.Memory.Photos.Any(p => p.PhotoId == photoId))
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in keys)
            _layer1.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task WarmAsync(IReadOnlyList<Memory> memories)
    {
        foreach (var m in memories)
            _layer1[m.Id] = (m, DateTime.UtcNow.AddHours(1));
        return Task.CompletedTask;
    }

    public void Clear() => _layer1.Clear();

    private void EvictIfNeeded()
    {
        if (_layer1.Count <= _config.Layer1Size) return;
        var expired = _layer1
            .Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in expired)
            _layer1.TryRemove(key, out _);
    }
}
