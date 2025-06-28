namespace com.aoyon.facetune;

internal record SessionContext
{
    public GameObject Root { get; }
    public SkinnedMeshRenderer FaceRenderer { get; }

    public Mesh FaceMesh { get; }
    public string BodyPath { get; }
    public List<BlendShape> ZeroWeightBlendShapes { get; }
    public List<string> EyeBlendShapeNames { get; }

    public SessionContext(
        GameObject root,
        SkinnedMeshRenderer faceRenderer,
        Mesh faceMesh,
        string bodyPath,
        List<BlendShape> zeroWeightBlendShapes,
        List<string> eyeBlendShapeNames
    )
    {
        Root = root;
        FaceRenderer = faceRenderer;
        FaceMesh = faceMesh;
        BodyPath = bodyPath;
        ZeroWeightBlendShapes = zeroWeightBlendShapes;
        EyeBlendShapeNames = eyeBlendShapeNames;
    }
}


