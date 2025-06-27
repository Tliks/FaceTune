using nadena.dev.ndmf;

namespace com.aoyon.facetune.ndmf;

internal class ApplyDefaulShapesPass : Pass<ApplyDefaulShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;

        /*
        var faceRenderer = buildPassContext.SessionContext.FaceRenderer;
        var faceMesh = buildPassContext.SessionContext.FaceMesh;
        var defaultBlendShapes = buildPassContext.SessionContext.ZeroWeightBlendShapes;

        MeshHelper.ApplyBlendShapes(faceRenderer, faceMesh, defaultBlendShapes);
        */
    }
}
