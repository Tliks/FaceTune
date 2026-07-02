namespace Aoyon.FaceTune.Build;

internal class ApplyDefaultShapesPass : FaceTunePass<ApplyDefaultShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(FaceTuneContext context)
    {
        var avatarContext = context.AvatarContext;
        var settings = context.BuildContext.GetState(_ => FaceTuneBuildSettings.Default);
        
        var facialStyleComponents = avatarContext.Root
            .GetComponentsInChildren<StyleComponent>(true)
            .Where(x => x.ApplyToRenderer);

        var componentCount = facialStyleComponents.Count();
        if (componentCount == 0) return;
        StyleComponent target;
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
        set.AddRange(avatarContext.FaceRenderer
            .GetBlendShapeWeights(avatarContext.FaceMesh)
            .Where(shape => !settings.ExcludedBlendShapeNames.Contains(shape.Name))
            .Select(shape => shape with { Weight = 0f }));
        ExpressionDataUtility.AddFirstFrameBlendShapes(target.Data, set, avatarContext.BodyPath);

        var renderer = avatarContext.FaceRenderer;
        var mesh = avatarContext.FaceMesh;
        renderer.ApplyBlendShapes(mesh, set, -1); 
    }
}
