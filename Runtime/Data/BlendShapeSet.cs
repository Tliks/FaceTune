namespace com.aoyon.facetune;

/// <summary>
/// 同名のBlendShapeを許容しないグループ
/// 結合や削除、差分の取りだしなど
/// </summary>
internal record class BlendShapeSet
{
    public readonly Dictionary<string, BlendShape> Mapping;
    public IEnumerable<BlendShape> BlendShapes => Mapping.Values;
    public IEnumerable<string> Names => Mapping.Keys;
    public IEnumerable<float> Weights => Mapping.Values.Select(x => x.Weight);
    public int Count => Mapping.Count;

    public BlendShapeSet()
    {
        Mapping = new Dictionary<string, BlendShape>();
    }

    public BlendShapeSet(IEnumerable<BlendShape> blendShapes, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        Mapping = new Dictionary<string, BlendShape>();
        Add(blendShapes, options);
    }

    public BlendShapeSet Duplicate()
    {
        return new BlendShapeSet(BlendShapes);
    }

    public BlendShapeSet Add(BlendShape blendShape, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
    {
        if (string.IsNullOrWhiteSpace(blendShape.Name)) return this;
        if (!Mapping.TryAdd(blendShape.Name, blendShape))
        {
            switch (options)
            {
                case BlendShapeSetOptions.PreferFormer:
                    break;
                case BlendShapeSetOptions.PreferLatter:
                    Mapping[blendShape.Name] = blendShape;
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
        Mapping.Remove(name);
        return this;
    }

    public BlendShapeSet Remove(IEnumerable<string> names)
    {
        Mapping.RemoveRange(names);
        return this;
    }

    public BlendShapeSet Remove(BlendShape blendShape)
    {
        Mapping.Remove(blendShape.Name);
        return this;
    }

    public BlendShapeSet Remove(IEnumerable<BlendShape> blendShapes)
    {
        Mapping.RemoveRange(blendShapes.Select(x => x.Name));
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
        if (Mapping.TryGetValue(oldName, out var blendShape))
        {
            Mapping.Remove(oldName);
            Mapping.Add(newName, new BlendShape(newName, blendShape.Weight));
        }
    }

    public BlendShapeSet RemoveZeroWeight()
    {
        var keysToRemove = Mapping
            .Where(x => x.Value.Weight == 0)
            .Select(x => x.Key)
            .ToList();

        Mapping.RemoveRange(keysToRemove);
        return this;
    }

    public BlendShape[] ToArrayForMesh(Mesh mesh, Func<int, float> defaultValueFactory)
    {
        var mapping = Mapping.Clone();
        var blendShapeCount = mesh.blendShapeCount;
        var blendShapes = new BlendShape[blendShapeCount];
        for (int i = 0; i < blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            blendShapes[i] = mapping.GetOrAdd(name, new BlendShape(name, defaultValueFactory(i)));
        }
        return blendShapes;
    }

    public BlendShapeSet ToDiff(BlendShapeSet baseSet, bool includeEqualOverride = false)
    {
        var diff = new BlendShapeSet();
        foreach (var blendShape in BlendShapes)
        {
            if (baseSet.Mapping.TryGetValue(blendShape.Name, out var baseValue))
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