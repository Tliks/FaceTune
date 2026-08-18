namespace Aoyon.FaceTune;

internal static partial class Utils
{
    public static Dictionary<string, string> CloneShapes(
        SkinnedMeshRenderer renderer,
        HashSet<string> shapesToClone,
        Action<Mesh, Mesh> onClone,
        Action<string> onNotFound,
        string suffix = "_clone")
    {
        var oldMesh = renderer.sharedMesh.DestroyedAsNull()
            ?? throw new ArgumentException("Renderer has no mesh.", nameof(renderer));
        var newMesh = Object.Instantiate(oldMesh);
        var mapping = CloneShapes(newMesh, shapesToClone, onNotFound, suffix);
        if (mapping.Count == 0)
        {
            Object.DestroyImmediate(newMesh);
            return mapping;
        }

        onClone(oldMesh, newMesh);
        renderer.sharedMesh = newMesh;
        return mapping;
    }

    public static Dictionary<string, string> CloneShapes(
        Mesh mesh,
        HashSet<string> shapesToClone,
        Action<string> onNotFound,
        string suffix = "_clone")
    {
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var existingNames = new HashSet<string>(StringComparer.Ordinal);
        var shapeIndices = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < mesh.blendShapeCount; index++)
        {
            var name = mesh.GetBlendShapeName(index);
            existingNames.Add(name);
            shapeIndices[name] = index;
        }

        var deltaVertices = new Vector3[mesh.vertexCount];
        var deltaNormals = new Vector3[mesh.vertexCount];
        var deltaTangents = new Vector3[mesh.vertexCount];

        foreach (var shape in shapesToClone)
        {
            if (!shapeIndices.TryGetValue(shape, out var shapeIndex))
            {
                onNotFound(shape);
                continue;
            }

            var cloneName = GetCloneName(shape, suffix, existingNames);
            existingNames.Add(cloneName);

            var frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
            for (var frame = 0; frame < frameCount; frame++)
            {
                mesh.GetBlendShapeFrameVertices(
                    shapeIndex,
                    frame,
                    deltaVertices,
                    deltaNormals,
                    deltaTangents);
                var frameWeight = mesh.GetBlendShapeFrameWeight(shapeIndex, frame);
                mesh.AddBlendShapeFrame(
                    cloneName,
                    frameWeight,
                    deltaVertices,
                    deltaNormals,
                    deltaTangents);
            }

            mapping.Add(shape, cloneName);
        }

        return mapping;
    }

    private static string GetCloneName(
        string sourceName,
        string suffix,
        ISet<string> existingNames)
    {
        for (var index = 1;; index++)
        {
            var number = index == 1 ? string.Empty : index.ToString();
            var candidate = $"{sourceName}{suffix}{number}";
            if (!existingNames.Contains(candidate)) return candidate;
        }
    }
}
