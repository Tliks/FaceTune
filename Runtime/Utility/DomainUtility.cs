namespace com.aoyon.facetune;

internal static class DomainUtility
{
    public static BlendShape[]? GetBlendShapes(this SkinnedMeshRenderer renderer)
    {
        var mesh = renderer.sharedMesh;
        if (mesh == null) return null;
        return GetBlendShapes(renderer, mesh);
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

    public static BlendShapeSet ToSet(this IReadOnlyCollection<BlendShape> blendShapes)
    {
        return new BlendShapeSet(blendShapes);
    }

    public static string[] Names(this BlendShape[] blendShapes)
    {
        return blendShapes.Select(x => x.Name).ToArray();
    }

    public static float[] Weights(this BlendShape[] blendShapes)
    {
        return blendShapes.Select(x => x.Weight).ToArray();
    }
}
