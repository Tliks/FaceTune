namespace Aoyon.FaceTune;

internal readonly record struct BlendShapeApply(
    IReadOnlyBlendShapeSet Set,
    float? DefaultValue = null,
    ImmutableHashSet<string>? IgnoredNames = null)
{
    public bool Equals(BlendShapeApply other)
    {
        if (DefaultValue != other.DefaultValue || !Set.Equals(other.Set))
            return false;

        if (ReferenceEquals(IgnoredNames, other.IgnoredNames)) return true;

        var leftCount = IgnoredNames?.Count ?? 0;
        var rightCount = other.IgnoredNames?.Count ?? 0;
        if (leftCount != rightCount) return false;
        if (leftCount == 0) return true;

        foreach (var name in IgnoredNames!)
        {
            if (!other.IgnoredNames!.Contains(name)) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var ignoredNamesHash = 0;
        if (IgnoredNames != null)
        {
            foreach (var name in IgnoredNames)
                ignoredNamesHash ^= StringComparer.Ordinal.GetHashCode(name);
        }

        return HashCode.Combine(Set, DefaultValue, ignoredNamesHash);
    }

    // false means that the current renderer value must be preserved.
    internal bool TryGetWeight(string name, out float weight)
    {
        if (IgnoredNames?.Contains(name) == true)
        {
            weight = default;
            return false;
        }

        if (Set.TryGetValue(name, out var blendShape))
        {
            weight = blendShape.Weight;
            return true;
        }

        if (DefaultValue is { } defaultValue)
        {
            weight = defaultValue;
            return true;
        }

        weight = default;
        return false;
    }
}

internal static partial class Utils
{
    public static BlendShapeWeight[] GetBlendShapeWeights(this SkinnedMeshRenderer renderer, Mesh mesh)
    {
        var blendShapes = new BlendShapeWeight[mesh.blendShapeCount];
        for (var i = 0; i < mesh.blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            var weight = renderer.GetBlendShapeWeight(i);
            blendShapes[i] = new BlendShapeWeight(name, weight);
        }
        return blendShapes;
    }

    public static IEnumerable<BlendShapeWeightAnimation> GetNonZeroBlendShapeAnimations(
        this SkinnedMeshRenderer renderer,
        Mesh mesh)
        => renderer.GetBlendShapeWeights(mesh)
            .Where(value => value.Weight != 0f)
            .ToBlendShapeAnimations();

    public static string[] GetBlendShapeNames(this Mesh mesh)
    {
        var blendShapes = new string[mesh.blendShapeCount];
        for (var i = 0; i < mesh.blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            blendShapes[i] = name;
        }
        return blendShapes;
    }

    public static void ApplyBlendShapes(this SkinnedMeshRenderer renderer, BlendShapeApply apply, Mesh mesh)
    {
        var blendShapeCount = mesh.blendShapeCount;
        for (var i = 0; i < blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            if (apply.TryGetWeight(name, out var weight))
                renderer.SetBlendShapeWeight(i, weight);
        }
    }
    
    public static BlendShapeWeightAnimation ToBlendShapeAnimation(this BlendShapeWeight blendShape)
    {
        return BlendShapeWeightAnimation.SingleFrame(blendShape.Name, blendShape.Weight);
    }

    public static IEnumerable<BlendShapeWeightAnimation> ToBlendShapeAnimations(this IEnumerable<BlendShapeWeight> blendShapes)
    {
        return blendShapes.Select(bs => bs.ToBlendShapeAnimation());
    }

    public static IEnumerable<BlendShapeWeight> ToFirstFrameBlendShapes(this IEnumerable<BlendShapeWeightAnimation> animations)
    {
        return animations.Select(a => a.ToFirstFrameBlendShape());
    }
}