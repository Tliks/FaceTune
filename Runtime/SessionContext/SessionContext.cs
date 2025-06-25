namespace com.aoyon.facetune;

internal record SessionContext
{
    public GameObject Root { get; }
    public SkinnedMeshRenderer FaceRenderer { get; }

    public Mesh FaceMesh { get; }
    public string BodyPath { get; }

    public SessionContext(
        GameObject root,
        SkinnedMeshRenderer faceRenderer,
        Mesh faceMesh,
        string bodyPath
    )
    {
        Root = root;
        FaceRenderer = faceRenderer;
        FaceMesh = faceMesh;
        BodyPath = bodyPath;
    }
}


