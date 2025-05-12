namespace com.aoyon.facetune;

[Serializable]
public struct BlendShape : IEqualityComparer<BlendShape>
{
    public string Name;
    public float Weight;

    public BlendShape()
    {
        Name = "";
        Weight = 0;
    }

    public BlendShape(string name, float weight)
    {
        Name = name;
        Weight = weight;
    }

    internal static BlendShape[]? GetShapesFor(SkinnedMeshRenderer renderer)
    {
        var mesh = renderer.sharedMesh;
        if (mesh == null) return null;
        return GetShapesFor(renderer, mesh);
    }

    internal static BlendShape[] GetShapesFor(SkinnedMeshRenderer renderer, Mesh mesh)
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

    public bool Equals(BlendShape other)
    {
        return other is BlendShape blendShape && Equals(blendShape);
    }

    public bool Equals(BlendShape x, BlendShape y)
    {
        return x.Name == y.Name && x.Weight.Equals(y.Weight);
    }

    public int GetHashCode(BlendShape obj)
    {
        return HashCode.Combine(obj.Name, obj.Weight);
    }
}