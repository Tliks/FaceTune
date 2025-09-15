using System;
using System.Collections.Generic;

namespace Aoyon.FaceTune;

internal static class DictionaryExtensions
{
    public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue addValue)
    {
        bool canAdd = !dict.ContainsKey(key);

        if (canAdd)
            dict.Add(key, addValue);

        return canAdd;
    }

    public static bool TryAddNew<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : new()
    {
        bool canAdd = !dict.ContainsKey(key);
        if (canAdd)
            dict.Add(key, new TValue());
        return canAdd;
    }

    public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> addValueFactory)
    {
        bool canAdd = !dict.ContainsKey(key);

        if (canAdd)
            dict.Add(key, addValueFactory(key));

        return canAdd;
    }

    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue addValue)
    {
        dict.TryAdd(key, addValue);
        return dict[key];
    }

    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : new()
    {
        dict.TryAddNew(key);
        return dict[key];
    }
    
    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> valueFactory)
    {
        dict.TryAdd(key, valueFactory);
        return dict[key];
    }

    public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> source, IEnumerable<KeyValuePair<TKey, TValue>> addPairs)
    {
        foreach (var kv in addPairs)
        {
            source.Add(kv);
        }
    }

    public static void RemoveRange<TKey, TValue>(this IDictionary<TKey, TValue> source, IEnumerable<TKey> removeKeys)
    {
        foreach (var key in removeKeys)
        {
            source.Remove(key);
        }
    }

    public static IDictionary<TKey, TValue> Clone<TKey, TValue>(this IDictionary<TKey, TValue> source)
    {
        return new Dictionary<TKey, TValue>(source);
    }
}