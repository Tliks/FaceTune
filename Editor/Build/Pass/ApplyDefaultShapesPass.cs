using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

internal class ApplyDefaultShapesPass : Pass<ApplyDefaultShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;

        var avatarContext = buildPassContext.AvatarContext;
        
        var facialStyleComponents = avatarContext.Root
            .GetComponentsInChildren<FacialStyleComponent>(true)
            .Where(x => x.ApplyToRenderer);

        var componentCount = facialStyleComponents.Count();
        if (componentCount == 0) return;
        FacialStyleComponent target;
        if (componentCount > 1)
        {
            LocalizedLog.Warning("Log:warning:ApplyDefaultShapesPass:MultipleFacialStyleComponentWithApplyToRenderer");
            target = facialStyleComponents.Last();
        }
        else
        {
            target = facialStyleComponents.First();
        }

        // 未知のブレンドシェイプを上書きせず、既知のブレンドシェイプのみ0で上書きする

        var set = new BlendShapeWeightSet();
        set.AddRange(avatarContext.ZeroBlendShapes);
        target.GetBlendShapes(set);

        var renderer = avatarContext.FaceRenderer;
        var mesh = avatarContext.FaceMesh;
        renderer.ApplyBlendShapes(mesh, set, -1); 
    }
}
