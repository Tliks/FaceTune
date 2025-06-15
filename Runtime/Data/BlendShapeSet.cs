using UnityEngine.Pool;

namespace com.aoyon.facetune;

/// <summary>
/// 同名のBlendShapeを許容しないグループ
/// 結合や削除、差分の取りだしなど
/// </summary>
internal class BlendShapeSet
{
    readonly Dictionary<string, BlendShape> map;
    public Dictionary<string, BlendShape>.ValueCollection BlendShapes => map.Values;
    public Dictionary<string, BlendShape>.KeyCollection Names => map.Keys;
    public int Count => map.Count;


    private BlendShapeSet(Dictionary<string, BlendShape> map)
    {
        this.map = map;
    }
    public BlendShapeSet(): this(new()) { }

    public BlendShapeSet(IEnumerable<BlendShape> blendShapes, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter) : this()
    {
        AddRange(blendShapes, options);
    }

    public BlendShapeSet Clone()
    {
        return new BlendShapeSet(new(map));
    }
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

    // otherが不変な保証がない
    public BlendShapeSet AddRange(BlendShapeSet other, BlendShapeSetOptions options = BlendShapeSetOptions.PreferLatter)
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

    public BlendShape[] ToArrayForMesh(Mesh mesh, Func<int, float> defaultValueFactory)
    {
        var mapping = map.Clone();
        var blendShapeCount = mesh.blendShapeCount;
        var blendShapes = new BlendShape[blendShapeCount];
        for (int i = 0; i < blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            blendShapes[i] = mapping.GetOrAdd(name, new BlendShape(name, defaultValueFactory(i)));
        }
        return blendShapes;
    }

    public BlendShapeSet Except(BlendShapeSet baseSet, bool includeEqualOverride = false)
    {
        var diff = new BlendShapeSet();
        foreach (var blendShape in BlendShapes)
        {
            if (baseSet.map.TryGetValue(blendShape.Name, out var baseValue))
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