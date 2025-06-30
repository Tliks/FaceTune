using UnityEngine.Pool;

namespace aoyon.facetune;

// unityEngineにCollectionPool/ListPool等は用意されているが、使用を区別し監視する為にこれを用いない
// 基本的に短命のオブジェクトに対する利用を想定しているためCapacityの制限は厳しい
internal class CollectionPool<TCollection, TItem> where TCollection : class, ICollection<TItem>, new()
{
    public static int CountActive => _pool.CountActive;
    public static int CountInactive => _pool.CountInactive;
    public static int CountAll => _pool.CountAll;

    private const int DefaultCapacity = 10;
    private const int MaxSize = 20;

    private static readonly ObjectPool<TCollection> _pool = new(
        createFunc: () => new TCollection(),
        actionOnGet: null,
        actionOnRelease: (collection) => collection.Clear(),
        actionOnDestroy: null,
        collectionCheck: true,
        defaultCapacity: DefaultCapacity,
        maxSize: MaxSize
    );

    // 極力こっちを使う
    public static PooledObject<TCollection> Get(out TCollection collection)
    {
        collection = Get();
        return new PooledObject<TCollection>(collection, _pool);
    }

    public static TCollection Get()
    {
        if (_pool.CountActive > DefaultCapacity)
        {
            Debug.LogWarning($"{typeof(TCollection).Name} is over DefaultCapacity. {_pool.CountActive} > {DefaultCapacity}");
        }
        return _pool.Get();
    }

    public static void Release(TCollection collection) => _pool.Release(collection);
}

internal sealed class ListPool<T> : CollectionPool<List<T>, T>
{
}

internal sealed class DictionaryPool<TKey, TValue> : CollectionPool<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>
{
}

internal sealed class HashSetPool<T> : CollectionPool<HashSet<T>, T>
{
}

internal sealed class BlendShapeSetPool : CollectionPool<BlendShapeSet, BlendShape>
{
}