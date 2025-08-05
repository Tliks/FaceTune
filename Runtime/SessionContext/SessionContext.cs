namespace Aoyon.FaceTune;

internal class SessionContext
{
    public readonly GameObject Root;
    public readonly SkinnedMeshRenderer FaceRenderer;
    
    public readonly Mesh FaceMesh;
    public readonly string BodyPath;
    public readonly IReadOnlyBlendShapeSet ZeroBlendShapes;
    public readonly HashSet<string> TrackedBlendShapes;
    public readonly IReadOnlyBlendShapeSet SafeZeroBlendShapes;

    public SessionContext(
        GameObject root,
        SkinnedMeshRenderer faceRenderer,
        Mesh faceMesh,
        string bodyPath,
        IReadOnlyBlendShapeSet zeroWeightBlendShapes,
        HashSet<string> trackedBlendShapes,
        IReadOnlyBlendShapeSet safeBlendShapes
    )
    {
        Root = root;
        FaceRenderer = faceRenderer;
        FaceMesh = faceMesh;
        BodyPath = bodyPath;
        ZeroBlendShapes = zeroWeightBlendShapes;
        TrackedBlendShapes = trackedBlendShapes;
        SafeZeroBlendShapes = safeBlendShapes;
    }
}


