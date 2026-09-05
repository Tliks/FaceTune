namespace Aoyon.FaceTune;

/// <summary>
/// 同名のBlendShapeWeightAnimationを許容しない集合
/// </summary>
internal class BlendShapeWeightAnimationSet : NamedItemSetBase<BlendShapeWeightAnimation, BlendShapeWeightAnimationSet>, IEquatable<BlendShapeWeightAnimationSet>
{
    protected override Func<BlendShapeWeightAnimation, string> KeySelector => static x => x.Name;

    public BlendShapeWeightAnimationSet() : base()
    {
    }
    public BlendShapeWeightAnimationSet(Dictionary<string, BlendShapeWeightAnimation> map) : base(map)
    {
    }
    public BlendShapeWeightAnimationSet(IEnumerable<BlendShapeWeightAnimation> items) : base(items)
    {   
    }
    
    public IEnumerable<string> GetBlendShapeNames() => Keys;

    public void ReplaceBlendShapeNames(Dictionary<string, string> mapping)
    {
        ReplaceKeys(mapping);
    }

    public IEnumerable<string> RemoveBlendShapes(HashSet<string> names)
    {
        var removed = Keys.Where(names.Contains).ToList();
        RemoveRange(removed);
        return removed;
    }

    public bool Equals(BlendShapeWeightAnimationSet? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Count != other.Count) return false;
        foreach (var animation in this)
        {
            if (!other.TryGetValue(animation.Name, out var otherAnimation)) return false;
            if (!animation.Equals(otherAnimation)) return false;
        }
        return true;
    }
    public override bool Equals(object? obj)
    {
        return obj is BlendShapeWeightAnimationSet set && Equals(set);
    }
    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var animation in this)
        {
            hash ^= HashCode.Combine(animation.Name, animation);
        }
        return HashCode.Combine(Count, hash);
    }
}