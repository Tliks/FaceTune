namespace aoyon.facetune;

internal static class MeshHelper
{
    public static Dictionary<string, string> CloneShapes(SkinnedMeshRenderer renderer, HashSet<string> shapesToClone, Action<Mesh, Mesh> onClone, Action<string> onNotFound, string suffix = "_clone")
    {
        var oldMesh = renderer.sharedMesh;
        var newMesh = Object.Instantiate(oldMesh);
        onClone(oldMesh, newMesh);
        var mapping = CloneShapes(newMesh, shapesToClone, onNotFound, suffix);
        renderer.sharedMesh = newMesh;
        return mapping;
    }

    public static Dictionary<string, string> CloneShapes(Mesh editableMesh, HashSet<string> shapesToClose, Action<string> onNotFound, string suffix = "_clone")
    {
        var mapping = new Dictionary<string, string>();
        var existingNames = new HashSet<string>();

        int blendShapeCount = editableMesh.blendShapeCount;
        var nameToIndex = new Dictionary<string, int>(blendShapeCount);
        for (int i = 0; i < blendShapeCount; i++)
        {
            var name = editableMesh.GetBlendShapeName(i);
            existingNames.Add(name);
            nameToIndex[name] = i;
        }

        var deltaVertices = new Vector3[editableMesh.vertexCount];
        var deltaNormals = new Vector3[editableMesh.vertexCount];
        var deltaTangents = new Vector3[editableMesh.vertexCount];

        foreach (var shape in shapesToClose)
        {
            if (nameToIndex.TryGetValue(shape, out int index))
            {
                string newShapeName;
                int counter = 1;
                do
                {
                    newShapeName = shape + "_clone" + (counter > 1 ? counter.ToString() : "");
                    counter++;
                } while (existingNames.Contains(newShapeName));

                existingNames.Add(newShapeName);

                int frameCount = editableMesh.GetBlendShapeFrameCount(index);
                for (int frame = 0; frame < frameCount; frame++)
                {
                    editableMesh.GetBlendShapeFrameVertices(index, frame, deltaVertices, deltaNormals, deltaTangents);
                    var frameWeight = editableMesh.GetBlendShapeFrameWeight(index, frame);
                    editableMesh.AddBlendShapeFrame(newShapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
                }
                mapping[shape] = newShapeName;
            }
            else
            {
                onNotFound(shape);
            }
        }
        return mapping;
    } 
}
