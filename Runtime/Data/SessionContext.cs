namespace com.aoyon.facetune;

internal record SessionContext
{
    public readonly GameObject Root;
    public readonly SkinnedMeshRenderer FaceRenderer;
    public readonly Mesh FaceMesh;
    public readonly FacialExpression DefaultExpression;
    public IEnumerable<BlendShape> DefaultBlendShapes => DefaultExpression.BlendShapeSet.BlendShapes;

    public SessionContext(GameObject root, SkinnedMeshRenderer faceRenderer, Mesh faceMesh, FacialExpression defaultExpression)
    {
        Root = root;
        FaceRenderer = faceRenderer;
        FaceMesh = faceMesh;
        DefaultExpression = defaultExpression;
    }
}