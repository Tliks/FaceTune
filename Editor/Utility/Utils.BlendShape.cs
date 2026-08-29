namespace Aoyon.FaceTune;

internal readonly record struct BlendShapeApply(
    SkinnedMeshRenderer Renderer,
    IReadOnlyBlendShapeSet Set,
    float? DefaultValue = null,
    ISet<string>? IgnoredNames = null)
{
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

    public static void ApplyBlendShapes(this BlendShapeApply apply, Mesh mesh)
    {
        var blendShapeCount = mesh.blendShapeCount;
        for (var i = 0; i < blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            if (apply.TryGetWeight(name, out var weight))
                apply.Renderer.SetBlendShapeWeight(i, weight);
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