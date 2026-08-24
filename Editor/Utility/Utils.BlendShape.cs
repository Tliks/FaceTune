namespace Aoyon.FaceTune;

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

    /// <summary>
    /// ブレンドシェイプを適用する
    /// defaultValueはblendShapeSetに含まれないブレンドシェイプのハンドリング
    /// -1のとき維持し、それ以外の場合は指定された値で上書き
    /// </summary>
    public static void ApplyBlendShapes(this SkinnedMeshRenderer renderer, Mesh mesh, IReadOnlyBlendShapeSet blendShapeSet, float defaultValue = -1)
    {
        var blendShapeCount = mesh.blendShapeCount;
        for (var i = 0; i < blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            if (blendShapeSet.TryGetValue(name, out var blendShape))
            {
                renderer.SetBlendShapeWeight(i, blendShape.Weight);
            }
            else if (defaultValue != -1)
            {
                renderer.SetBlendShapeWeight(i, defaultValue);
            }
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