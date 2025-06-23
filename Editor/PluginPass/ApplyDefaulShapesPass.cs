using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class ApplyDefaulShapesPass : Pass<ApplyDefaulShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.Extension<FTPassContext>()!;
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;

        var faceRenderer = sessionContext.FaceRenderer;
        var faceMesh = sessionContext.FaceMesh;
        var defaultBlendShapes = sessionContext.DEC.GetGlobalDefaultBlendShapeSet().BlendShapes;

        MeshHelper.ApplyBlendShapes(faceRenderer, faceMesh, defaultBlendShapes);
    }
}
