namespace aoyon.facetune;

/// <summary>
/// 同名のBlendShapeを許容しないグループ
/// 結合や削除、差分の取りだしなど
/// </summary>
internal class BlendShapeSet : ICollection<BlendShape>, IEquatable<BlendShapeSet>, IReadOnlyBlendShapeSet
{
    readonly Dictionary<string, BlendShape> map;
    public Dictionary<string, BlendShape>.ValueCollection BlendShapes => map.Values;
    public Dictionary<string, BlendShape>.KeyCollection Names => map.Keys;
    public int Count => map.Count;

    public bool IsReadOnly => false;

    private BlendShapeSet(Dictionary<string, BlendShape> map)
    {
        this.map = map;
    }
    public BlendShapeSet(): this(new()) { }

    public BlendShapeSet(IEnumerable<BlendShape> blendShapes, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter) : this()
    {
        AddRange(blendShapes, options);
    }

    public IEnumerator<BlendShape> GetEnumerator()
    {
        return map.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Clear()
    {
        map.Clear();
    }

    public BlendShapeSet Clone()
    {
        return new BlendShapeSet(new(map));
    }

    public void CloneTo(BlendShapeSet result)
    {
        foreach (var (_, blendShape) in map)
        {
            result.Add(blendShape);
        }
    }

    public bool Contains(string name) => map.ContainsKey(name);
    public bool Contains(BlendShape item) => map.TryGetValue(item.Name, out var value) && value.Weight == item.Weight;
    public bool TryGetValue(string key, out BlendShape value) => map.TryGetValue(key, out value);
    public BlendShapeSet Add(BlendShape blendShape, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        if (string.IsNullOrWhiteSpace(blendShape.Name)) return this;

        switch (options)
        {
            case BlendShapeSetOptions.PreferFormer:
                {
                    map.TryAdd(blendShape.Name, blendShape);
                }
                break;
            case BlendShapeSetOptions.PreferLatter:
                {
                    map[blendShape.Name] = blendShape;
                }
                break;
            case BlendShapeSetOptions.ThrowException:
                {
                    map.Add(blendShape.Name, blendShape);
                }
                break;
        }
        return this;
    }

    public void Add(BlendShape item)
    {
        Add(item, BlendShapeSetOptions.PreferLatter); 
    }

    public BlendShapeSet AddRange(IReadOnlyBlendShapeSet other, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        foreach (var blendShape in other.BlendShapes)
        {
            Add(blendShape, options);
        }
        return this;
    }

    public BlendShapeSet AddRange(IEnumerable<BlendShape> blendShapes, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        foreach (var blendShape in blendShapes)
        {
            Add(blendShape, options);
        }
        return this;
    }

    bool ICollection<BlendShape>.Remove(BlendShape item)
    {
        return map.Remove(item.Name);
    }

    public BlendShapeSet Remove(string name)
    {
        map.Remove(name);
        return this;
    }

    public BlendShapeSet RemoveRange(IEnumerable<string> names)
    {
        map.RemoveRange(names);
        return this;
    }

    public BlendShapeSet Remove(BlendShape blendShape)
    {
        map.Remove(blendShape.Name);
        return this;
    }

    public BlendShapeSet RemoveRange(IEnumerable<BlendShape> blendShapes)
    {
        map.RemoveRange(blendShapes.Select(x => x.Name));
        return this;
    }

    public void ReplaceNames(Dictionary<string, string> mapping)
    {
        foreach (var (oldName, newName) in mapping)
        {
            ReplaceName(oldName, newName);
        }
    }

    public void ReplaceName(string oldName, string newName)
    {
        if (map.TryGetValue(oldName, out var blendShape))
        {
            map.Remove(oldName);
            map.Add(newName, new BlendShape(newName, blendShape.Weight));
        }
    }

    public void SetAllWeight(float weight)
    {
        foreach (var (name, _) in map)
        {
            map[name] = new BlendShape(name, weight);
        }
    }

    public BlendShapeSet RemoveZeroWeight()
    {
        using (ListPool<string>.Get(out var keysToRemove))
        {
            foreach (var keyValue in map)
            {
                if (keyValue.Value.Weight == 0)
                {
                    keysToRemove.Add(keyValue.Key);
                }
            }

            map.RemoveRange(keysToRemove);
        }

        return this;
    }

    public BlendShapeSet Where(Func<BlendShape, bool> predicate)
    {
        var result = new BlendShapeSet();
        foreach (var blendShape in BlendShapes)
        {
            if (predicate(blendShape))
            {
                result.Add(blendShape);
            }
        }
        return result;
    }

    public IReadOnlyBlendShapeSet AsReadOnly()
    {
        return this;
    }

    public BlendShapeSet Except(BlendShapeSet baseSet, bool includeEqualOverride = false)
    {
        return Except(baseSet.AsReadOnly(), includeEqualOverride);
    }

    public void CopyTo(BlendShape[] array, int arrayIndex)
    {
        map.Values.CopyTo(array, arrayIndex);
    }

    public bool Equals(BlendShapeSet other)
    {
        if (other is null) return false;
        return Equals(other.AsReadOnly());
    }

    public override bool Equals(object? obj)
    {
        return obj is BlendShapeSet set && Equals(set);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (name, blendShape) in map.OrderBy(x => x.Key))
        {
            hash.Add(name);
            hash.Add(blendShape.Weight);
        }
        return hash.ToHashCode();
    }

    public BlendShapeSet Except(IReadOnlyBlendShapeSet baseSet, bool includeEqualOverride = false)
    {
        var diff = new BlendShapeSet();
        foreach (var blendShape in BlendShapes)
        {
            if (baseSet.TryGetValue(blendShape.Name, out var baseValue))
            {
                if (includeEqualOverride || baseValue.Weight != blendShape.Weight)
                {
                    diff.Add(blendShape);
                }
            }
            else
            {
                diff.Add(blendShape);
            }
        }
        return diff;
    }



    public bool Equals(IReadOnlyBlendShapeSet other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Count != other.Count) return false;

        foreach (var (name, blendShape) in map)
        {
            if (!other.TryGetValue(name, out var otherBlendShape)) return false;
            if (blendShape.Weight != otherBlendShape.Weight) return false;
        }
        return true;
    }
}

internal interface IReadOnlyBlendShapeSet : IReadOnlyCollection<BlendShape>, IEquatable<IReadOnlyBlendShapeSet>
{
    Dictionary<string, BlendShape>.ValueCollection BlendShapes { get; }
    Dictionary<string, BlendShape>.KeyCollection Names { get; }
    BlendShapeSet Clone();
    void CloneTo(BlendShapeSet result);
    bool Contains(string name);
    bool Contains(BlendShape item);
    bool TryGetValue(string key, out BlendShape value);
    BlendShapeSet Except(IReadOnlyBlendShapeSet baseSet, bool includeEqualOverride = false);
    BlendShapeSet Where(Func<BlendShape, bool> predicate);
}

internal enum BlendShapeSetOptions
{
    PreferFormer,
    PreferLatter,
    ThrowException
}