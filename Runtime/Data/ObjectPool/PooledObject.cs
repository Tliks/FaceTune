using UnityEngine.Pool;

namespace com.aoyon.facetune;

internal sealed class PooledObject<T> : IDisposable where T : class
{
    public T Value { get; }
    private readonly IObjectPool<T> _pool;
    public bool Disposed { get; private set; } = false;

    internal PooledObject(T value, IObjectPool<T> pool)
    {
        Value = value;
        _pool = pool;
    }

    public void Dispose()
    {
        if (!Disposed)
        {
            _pool.Release(Value);
            Disposed = true;
        }
    }
}