namespace PhotoSort.Services;

public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly LinkedList<LruCacheItem<TKey, TValue>> _list;
    private readonly Dictionary<TKey, LinkedListNode<LruCacheItem<TKey, TValue>>> _map;
    private readonly SemaphoreSlim _lock;
    private bool _disposed;

    public LruCache(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _list = new LinkedList<LruCacheItem<TKey, TValue>>();
        _map = new Dictionary<TKey, LinkedListNode<LruCacheItem<TKey, TValue>>>(capacity);
        _lock = new SemaphoreSlim(1, 1);
    }

    public int Count
    {
        get
        {
            _lock.Wait();
            try { return _map.Count; }
            finally { _lock.Release(); }
        }
    }

    public TValue? Get(TKey key)
    {
        _lock.Wait();
        try
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                return node.Value.Value;
            }
            return default;
        }
        finally
        {
            _lock.Release();
        }
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        _lock.Wait();
        try
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _list.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
            value = default;
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Put(TKey key, TValue value)
    {
        _lock.Wait();
        try
        {
            if (_map.TryGetValue(key, out var node))
            {
                node.Value.Value = value;
                _list.Remove(node);
                _list.AddFirst(node);
                return;
            }

            if (_map.Count >= _capacity)
            {
                var last = _list.Last;
                if (last is not null)
                {
                    _map.Remove(last.Value.Key);
                    _list.RemoveLast();
                }
            }

            var item = new LruCacheItem<TKey, TValue> { Key = key, Value = value };
            var newNode = _list.AddFirst(item);
            _map[key] = newNode;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Remove(TKey key)
    {
        _lock.Wait();
        try
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _map.Remove(key);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Clear()
    {
        _lock.Wait();
        try
        {
            _list.Clear();
            _map.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Clear(Action<TValue> cleanup)
    {
        _lock.Wait();
        try
        {
            foreach (var item in _list)
            {
                cleanup(item.Value);
            }
            _list.Clear();
            _map.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    public IReadOnlyList<TKey> GetKeys()
    {
        _lock.Wait();
        try
        {
            return _map.Keys.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
    }

    private sealed class LruCacheItem<TK, TV>
    {
        public TK Key { get; init; } = default!;
        public TV Value { get; set; } = default!;
    }
}
