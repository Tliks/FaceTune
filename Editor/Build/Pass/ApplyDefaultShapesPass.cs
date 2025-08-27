using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

internal class ApplyDefaultShapesPass : Pass<ApplyDefaultShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;

        var sessionContext = buildPassContext.SessionContext;
        
        var facialStyleComponents = sessionContext.Root
            .GetComponentsInChildren<FacialStyleComponent>(true)
            .Where(x => x.ApplyToRenderer);

        var componentCount = facialStyleComponents.Count();
        if (componentCount == 0) return;
        FacialStyleComponent target;
        if (componentCount > 1)
        {
            Debug.LogWarning("ApplyDefaultShapesPass: Multiple FacialStyleComponent with ApplyToRenderer are found");
            target = facialStyleComponents.Last();
        }
        else
        {
            target = facialStyleComponents.First();
        }

        // 未知のブレンドシェイプを上書きせず、既知のブレンドシェイプのみ0で上書きする

        var set = new BlendShapeSet();
        set.AddRange(sessionContext.ZeroBlendShapes);
        target.GetBlendShapes(set);

        var renderer = sessionContext.FaceRenderer;
        var mesh = sessionContext.FaceMesh;
        renderer.ApplyBlendShapes(mesh, set, -1); 
    }
}
