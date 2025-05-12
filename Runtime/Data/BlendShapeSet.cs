namespace com.aoyon.facetune;


/// <summary>
/// 同名のBlendShapeを許容しないグループ
/// </summary>
internal class BlendShapeSet
{
    public IEnumerable<BlendShape> BlendShapes { get => _mapping.Values; }
    private readonly Dictionary<string, BlendShape> _mapping;

    internal BlendShapeSet()
    {
        _mapping = new Dictionary<string, BlendShape>();
    }

    internal BlendShapeSet(List<BlendShape> blendShapes, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        _mapping = new Dictionary<string, BlendShape>();
        AddRange(blendShapes, options);
    }
    
    internal BlendShapeSet Add(BlendShape blendShape, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
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

    internal BlendShapeSet Merge(BlendShapeSet other, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        foreach (var blendShape in other.BlendShapes)
        {
            Add(blendShape, options);
        }
        return this;
    }

    internal void AddRange(List<BlendShape> blendShapes, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        foreach (var blendShape in blendShapes)
        {
            Add(blendShape, options);
        }
    }

    internal bool Remove(string name)
    {
        return _mapping.Remove(name);
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