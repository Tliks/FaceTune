namespace Aoyon.FaceTune;

internal static class BlendShapeUtility
{
    public static void GetBlendShapes(this SkinnedMeshRenderer renderer, ICollection<BlendShape> resultToAdd)
    {
        for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
        {
            var name = renderer.sharedMesh.GetBlendShapeName(i);
            var weight = renderer.GetBlendShapeWeight(i);
            resultToAdd.Add(new BlendShape(name, weight));
        }
    }
    
    public static void GetBlendShapesAndSetWeightToZero(this SkinnedMeshRenderer renderer, ICollection<BlendShape> resultToAdd)
    {
        for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
        {
            var name = renderer.sharedMesh.GetBlendShapeName(i);
            resultToAdd.Add(new BlendShape(name, 0f));
        }
    }

    public static BlendShape[] GetBlendShapes(this SkinnedMeshRenderer renderer, Mesh mesh)
    {
        var blendShapes = new BlendShape[mesh.blendShapeCount];
        for (var i = 0; i < mesh.blendShapeCount; i++)
        {
            var name = mesh.GetBlendShapeName(i);
            var weight = renderer.GetBlendShapeWeight(i);
            blendShapes[i] = new BlendShape(name, weight);
        }
        return blendShapes;
    }

    public static IEnumerable<BlendShapeAnimation> ToBlendShapeAnimations(this IEnumerable<BlendShape> blendShapes)
    {
        return blendShapes.Select(bs => BlendShapeAnimation.SingleFrame(bs.Name, bs.Weight));
    }

    public static IEnumerable<BlendShape> ToFirstFrameBlendShapes(this IEnumerable<BlendShapeAnimation> animations)
    {
        return animations.Select(a => a.ToFirstFrameBlendShape());
    }

    public static IEnumerable<GenericAnimation> ToGenericAnimations(this IEnumerable<BlendShapeAnimation> blendShapes, string path)
    {
        return blendShapes.Select(bs => bs.ToGeneric(path));
    }

    public static IEnumerable<GenericAnimation> ToGenericAnimations(this IEnumerable<BlendShape> blendShapes, string path)
    {
        return blendShapes.ToBlendShapeAnimations().Select(bs => bs.ToGeneric(path));
    }
}