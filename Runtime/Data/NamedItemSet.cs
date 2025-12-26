namespace Aoyon.FaceTune;

// 汎用の名前付きアイテム集合基底。
internal abstract class NamedItemSetBase<TItem, TSelf> : ICollection<TItem>, IReadOnlyCollection<TItem> where TSelf : NamedItemSetBase<TItem, TSelf>, new()
{
    protected abstract Func<TItem, string> keySelector { get; }

    protected readonly Dictionary<string, TItem> map;

    public Dictionary<string, TItem>.ValueCollection Values => map.Values;
    public Dictionary<string, TItem>.KeyCollection Keys => map.Keys;
    public int Count => map.Count;
    public bool IsReadOnly => false;

    protected NamedItemSetBase(Dictionary<string, TItem> map)
    {
        this.map = map;
    }

    protected NamedItemSetBase() : this(new Dictionary<string, TItem>()) { }

    protected NamedItemSetBase(IEnumerable<TItem> items, NamedItemSetOptions options = NamedItemSetOptions.PreferLatter) : this(new Dictionary<string, TItem>())
    {
        AddRange(items, options);
    }

    public IEnumerator<TItem> GetEnumerator() => map.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Clear() => map.Clear();

    public bool ContainsKey(string key) => map.ContainsKey(key);

    public bool Contains(TItem item)
    {
        var key = keySelector(item);
        return map.TryGetValue(key, out var existing) && EqualityComparer<TItem>.Default.Equals(existing, item);
    }

    public bool TryGetValue(string key, out TItem value) => map.TryGetValue(key, out value);

    public TSelf Add(TItem item, NamedItemSetOptions options = NamedItemSetOptions.PreferLatter)
    {
        var key = keySelector(item);
        if (string.IsNullOrWhiteSpace(key)) return (TSelf)this;

        switch (options)
        {
            case NamedItemSetOptions.PreferFormer:
                map.TryAdd(key, item);
                break;
            case NamedItemSetOptions.PreferLatter:
                map[key] = item;
                break;
            case NamedItemSetOptions.ThrowException:
                map.Add(key, item);
                break;
        }
        return (TSelf)this;
    }

    void ICollection<TItem>.Add(TItem item)
    {
        Add(item, NamedItemSetOptions.PreferLatter);
    }

    public void Add(TItem item)
    {
        Add(item, NamedItemSetOptions.PreferLatter);
    }

    public TSelf AddRange(IEnumerable<TItem> items, NamedItemSetOptions options = NamedItemSetOptions.PreferLatter)
    {
        foreach (var item in items)
        {
            Add(item, options);
        }
        return (TSelf)this;
    }

    public TSelf Remove(string key)
    {
        map.Remove(key);
        return (TSelf)this;
    }

    public TSelf Remove(TItem item)
    {
        map.Remove(keySelector(item));
        return (TSelf)this;
    }

    public TSelf RemoveRange(IEnumerable<string> keys)
    {
        map.RemoveRange(keys);
        return (TSelf)this;
    }

    public TSelf RemoveRange(IEnumerable<TItem> items)
    {
        map.RemoveRange(items.Select(x => keySelector(x)));
        return (TSelf)this;
    }

    public void CopyTo(TItem[] array, int arrayIndex)
    {
        map.Values.CopyTo(array, arrayIndex);
    }

    bool ICollection<TItem>.Remove(TItem item)
    {
        return map.Remove(keySelector(item));
    }

    public TSelf Clone()
    {
        var newSet = new TSelf();
        newSet.AddRange(map.Values);
        return newSet;
    }

    public void CloneTo(TSelf other)
    {
        other.AddRange(map.Values);
    }

    public TSelf Where(Func<TItem, bool> predicate)
    {
        var newSet = new TSelf();
        newSet.AddRange(map.Values.Where(predicate));
        return newSet;
    }

    public void ReplaceKey(string oldKey, string newKey)
    {
        if (map.TryGetValue(oldKey, out var item))
        {
            map.Remove(oldKey);
            map.Add(newKey, item);
        }
    }

    public void ReplaceKeys(Dictionary<string, string> mapping)
    {
        foreach (var (oldKey, newKey) in mapping)
        {
            ReplaceKey(oldKey, newKey);
        }
    }
}

internal enum NamedItemSetOptions
{
    PreferFormer,
    PreferLatter,
    ThrowException
}