namespace Aoyon.FaceTune;

/// <summary>
/// 同名のBlendShapeWeightを許容しない集合
/// </summary>
internal class BlendShapeWeightSet : NamedItemSetBase<BlendShapeWeight, BlendShapeWeightSet>, IEquatable<BlendShapeWeightSet>
{
    protected override Func<BlendShapeWeight, string> KeySelector => static x => x.Name;

    public BlendShapeWeightSet() : base()
    {
    }

    public BlendShapeWeightSet(Dictionary<string, BlendShapeWeight> map) : base(map)
    {
    }

    public BlendShapeWeightSet(IEnumerable<BlendShapeWeight> blendShapes) : base(blendShapes)
    {
    }

    public override bool Equals(object? obj)
    {
        return obj is BlendShapeWeightSet set && Equals(set);
    }
    
    public bool Equals(BlendShapeWeightSet other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Count != other.Count) return false;

        foreach (var blendShape in this)
        {
            if (!other.TryGetValue(blendShape.Name, out var otherBlendShape)) return false;
            if (blendShape.Weight != otherBlendShape.Weight) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var blendShape in this)
        {
            hash ^= HashCode.Combine(blendShape.Name, blendShape.Weight);
        }
        return HashCode.Combine(Count, hash);
    }

}

internal sealed class ImmutableBlendShapeWeightSet : ReadOnlyNamedItemSetBase<BlendShapeWeight>, IEquatable<ImmutableBlendShapeWeightSet>
{
    public ImmutableBlendShapeWeightSet()
    {
    }

    public ImmutableBlendShapeWeightSet(IEnumerable<BlendShapeWeight> blendShapes)
        : base(blendShapes)
    {
    }

    internal ImmutableBlendShapeWeightSet(IEnumerable<BlendShapeWeight> blendShapes, int capacity)
        : base(blendShapes, capacity)
    {
    }

    protected override Func<BlendShapeWeight, string> KeySelector => static x => x.Name;

    public bool Equals(ImmutableBlendShapeWeightSet? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Count != other.Count) return false;

        foreach (var blendShape in this)
        {
            if (!other.TryGetValue(blendShape.Name, out var otherBlendShape)) return false;
            if (blendShape.Weight != otherBlendShape.Weight) return false;
        }
        return true;
    }

    public override bool Equals(object? obj)
        => obj is ImmutableBlendShapeWeightSet other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var blendShape in this)
            hash ^= HashCode.Combine(blendShape.Name, blendShape.Weight);
        return HashCode.Combine(Count, hash);
    }
}
