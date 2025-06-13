namespace com.aoyon.facetune;

/// <summary>
/// 同名のBlendShapeを許容しないグループ
/// 結合や削除、差分の取りだしなど
/// </summary>
internal record class BlendShapeSet : IEnumerable<BlendShape>
{
    private readonly Dictionary<string, BlendShape> _mapping;
    public ReadOnlyDictionary<string, BlendShape> GetMapping() => new(_mapping);
    public IEnumerable<BlendShape> BlendShapes => _mapping.Values;
    public IEnumerable<string> Names => _mapping.Keys;
    public IEnumerable<float> Weights => _mapping.Values.Select(x => x.Weight);
    public int Count => _mapping.Count;

    public BlendShapeSet()
    {
        _mapping = new Dictionary<string, BlendShape>();
    }

    public BlendShapeSet(IEnumerable<BlendShape> blendShapes, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        _mapping = new Dictionary<string, BlendShape>();
        Add(blendShapes, options);
    }

    public BlendShapeSet Duplicate()
    {
        return new BlendShapeSet(BlendShapes);
    }

    public IEnumerator<BlendShape> GetEnumerator()
    {
        return _mapping.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public BlendShapeSet Add(BlendShape blendShape, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        if (string.IsNullOrWhiteSpace(blendShape.Name)) return this;
        if (!_mapping.TryAdd(blendShape.Name, blendShape))
        {
            switch (options)
            {
                case BlendShapeSetOptions.PreferFormer:
                    break;
                case BlendShapeSetOptions.PreferLatter:
                    _mapping[blendShape.Name] = blendShape;
                    break;
                case BlendShapeSetOptions.ThrowException:
                    throw new ArgumentException($"BlendShape name '{blendShape.Name}' is duplicated.");
            }
        }
        return this;
    }

    // otherが不変な保証がない
    public BlendShapeSet Add(BlendShapeSet other, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        foreach (var blendShape in other.BlendShapes)
        {
            Add(blendShape, options);
        }
        return this;
    }

    public BlendShapeSet Add(IEnumerable<BlendShape> blendShapes, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        foreach (var blendShape in blendShapes)
        {
            Add(blendShape, options);
        }
        return this;
    }

    public BlendShapeSet Remove(string name)
    {
        _mapping.Remove(name);
        return this;
    }

    public BlendShapeSet Remove(IEnumerable<string> names)
    {
        _mapping.RemoveRange(names);
        return this;
    }

    public BlendShapeSet Remove(BlendShape blendShape)
    {
        _mapping.Remove(blendShape.Name);
        return this;
    }

    public BlendShapeSet Remove(IEnumerable<BlendShape> blendShapes)
    {
        _mapping.RemoveRange(blendShapes.Select(x => x.Name));
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
        if (_mapping.TryGetValue(oldName, out var blendShape))
        {
            _mapping.Remove(oldName);
            _mapping.Add(newName, new BlendShape(newName, blendShape.Weight));
        }
    }

    public BlendShapeSet RemoveZeroWeight()
    {
        var keysToRemove = _mapping
            .Where(x => x.Value.Weight == 0)
            .Select(x => x.Key)
            .ToList();

        _mapping.RemoveRange(keysToRemove);
        return this;
    }

    public BlendShapeSet ToDiff(BlendShapeSet baseSet, bool includeEqualOverride = false)
    {
        var diff = new BlendShapeSet();
        foreach (var blendShape in BlendShapes)
        {
            if (baseSet._mapping.TryGetValue(blendShape.Name, out var baseValue))
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
}

/// <summary>
/// 同名のBlendShapeがあった際に前後どちらを優先するか、エラーを出すか
/// </summary>
internal enum BlendShapeSetOptions
{
    PreferFormer,
    PreferLatter,
    ThrowException
}