using UnityEngine.Pool;

namespace com.aoyon.facetune;

internal sealed class PooledObject<T> : IDisposable where T : class
{
    public T Value { get; }
    private readonly IObjectPool<T> _pool;
    private bool _disposed;

    internal PooledObject(T value, IObjectPool<T> pool)
    {
        Value = value;
        _pool = pool;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _pool.Release(Value);
            _disposed = true;
        }
    }
}