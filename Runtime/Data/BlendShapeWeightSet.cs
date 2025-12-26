namespace Aoyon.FaceTune;

/// <summary>
/// 同名のBlendShapeWeightを許容しない集合
/// </summary>
internal class BlendShapeWeightSet : NamedItemSetBase<BlendShapeWeight, BlendShapeWeightSet>, IEquatable<BlendShapeWeightSet>, IReadOnlyBlendShapeSet
{
    protected override Func<BlendShapeWeight, string> keySelector => static x => x.Name;

    public BlendShapeWeightSet() : base()
    {
    }

    public BlendShapeWeightSet(Dictionary<string, BlendShapeWeight> map) : base(map)
    {
    }

    public BlendShapeWeightSet(IEnumerable<BlendShapeWeight> blendShapes, NamedItemSetOptions options = NamedItemSetOptions.PreferLatter) : base(blendShapes, options)
    {
    }

    public IReadOnlyBlendShapeSet AsReadOnly()
    {
        return this;
    }

    public override bool Equals(object? obj)
    {
        return obj is BlendShapeWeightSet set && Equals(set);
    }
    
    public bool Equals(BlendShapeWeightSet other)
    {
        if (other is null) return false;
        return Equals(other.AsReadOnly());
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

internal interface IReadOnlyBlendShapeSet : IReadOnlyCollection<BlendShapeWeight>, IEquatable<IReadOnlyBlendShapeSet>
{
    Dictionary<string, BlendShapeWeight>.ValueCollection Values { get; }
    Dictionary<string, BlendShapeWeight>.KeyCollection Keys { get; }
    BlendShapeWeightSet Clone();
    void CloneTo(BlendShapeWeightSet result);
    bool ContainsKey(string name);
    bool Contains(BlendShapeWeight item);
    bool TryGetValue(string key, out BlendShapeWeight value);
    BlendShapeWeightSet Where(Func<BlendShapeWeight, bool> predicate);
}
