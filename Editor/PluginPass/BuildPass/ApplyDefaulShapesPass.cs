using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ApplyDefaulShapesPass : Pass<ApplyDefaulShapesPass>
{
    public override string QualifiedName => "com.aoyon.facetune.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(BuildContext context)
    {
        var sessionContext = context.Extension<BuildPassContext>().SessionContext;
        if (sessionContext == null) return;

        var faceRenderer = sessionContext.FaceRenderer;
        var defaultBlendShapes = sessionContext.DefaultBlendShapes;

        MeshHelper.ApplyBlendShapes(faceRenderer, sessionContext.FaceMesh, defaultBlendShapes);
    }
}
