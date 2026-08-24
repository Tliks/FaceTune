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
        foreach (var (name, anim) in map)
        {
            if (!other.map.TryGetValue(name, out var otherAnim)) return false;
            if (!anim.Equals(otherAnim)) return false;
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
        foreach (var (name, anim) in map)
        {
            hash ^= HashCode.Combine(name, anim);
        }
        return HashCode.Combine(Count, hash);
    }
}