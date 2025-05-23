namespace com.aoyon.facetune.pass;

internal class ApplyDefaulShapesPass : AbstractBuildPass<ApplyDefaulShapesPass>
{
    public override string QualifiedName => "com.aoyon.facetune.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void ExecuteCore(BuildPassContext context)
    {
        var sessionContext = context.SessionContext;

        var faceRenderer = sessionContext.FaceRenderer;
        var defaultBlendShapes = sessionContext.DefaultBlendShapes;

        MeshHelper.ApplyBlendShapes(faceRenderer, sessionContext.FaceMesh, defaultBlendShapes);
    }
}
