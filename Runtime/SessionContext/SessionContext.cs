namespace aoyon.facetune;

internal class SessionContext
{
    public readonly GameObject Root;
    public readonly SkinnedMeshRenderer FaceRenderer;
    
    public readonly Mesh FaceMesh;
    public readonly string BodyPath;
    public readonly List<BlendShape> ZeroWeightBlendShapes;
    public readonly HashSet<string> TrackedBlendShapes;

    public SessionContext(
        GameObject root,
        SkinnedMeshRenderer faceRenderer,
        Mesh faceMesh,
        string bodyPath,
        List<BlendShape> zeroWeightBlendShapes,
        HashSet<string> trackedBlendShapes
    )
    {
        Root = root;
        FaceRenderer = faceRenderer;
        FaceMesh = faceMesh;
        BodyPath = bodyPath;
        ZeroWeightBlendShapes = zeroWeightBlendShapes;
        TrackedBlendShapes = trackedBlendShapes;
    }
}


