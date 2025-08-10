namespace Aoyon.FaceTune;

internal static class BlendShapeUtility
{
    public static void GetBlendShapes(this SkinnedMeshRenderer renderer, ICollection<BlendShapeWeight> resultToAdd)
    {
        for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
        {
            var name = renderer.sharedMesh.GetBlendShapeName(i);
            var weight = renderer.GetBlendShapeWeight(i);
            resultToAdd.Add(new BlendShapeWeight(name, weight));
        }
    }
    
    public static void GetBlendShapesAndSetWeightToZero(this SkinnedMeshRenderer renderer, ICollection<BlendShapeWeight> resultToAdd)
    {
        for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
        {
            var name = renderer.sharedMesh.GetBlendShapeName(i);
            resultToAdd.Add(new BlendShapeWeight(name, 0f));
        }
    }

    public static BlendShapeWeight[] GetBlendShapes(this SkinnedMeshRenderer renderer, Mesh mesh)
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

    
    public static BlendShapeWeightAnimation ToBlendShapeAnimation(this BlendShapeWeight blendShape)
    {
        return BlendShapeWeightAnimation.SingleFrame(blendShape.Name, blendShape.Weight);
    }

    public static IEnumerable<BlendShapeWeightAnimation> ToBlendShapeAnimations(this IEnumerable<BlendShapeWeight> blendShapes)
    {
        return blendShapes.Select(bs => bs.ToBlendShapeAnimation());
    }

    public static GenericAnimation ToGenericAnimation(this BlendShapeWeight blendShape, string path)
    {
        return blendShape.ToBlendShapeAnimation().ToGeneric(path);
    }

    public static IEnumerable<GenericAnimation> ToGenericAnimations(this IEnumerable<BlendShapeWeight> blendShapes, string path)
    {
        return blendShapes.Select(bs => bs.ToGenericAnimation(path));
    }

    public static IEnumerable<BlendShapeWeight> ToFirstFrameBlendShapes(this IEnumerable<BlendShapeWeightAnimation> animations)
    {
        return animations.Select(a => a.ToFirstFrameBlendShape());
    }

    public static IEnumerable<GenericAnimation> ToGenericAnimations(this IEnumerable<BlendShapeWeightAnimation> blendShapes, string path)
    {
        return blendShapes.Select(bs => bs.ToGeneric(path));
    }
}