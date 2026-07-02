namespace Aoyon.FaceTune.Build;

internal class ApplyDefaultShapesPass : FaceTunePass<ApplyDefaultShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(FaceTuneContext context)
    {
        var avatarContext = context.AvatarContext;
        var settings = context.RequireSettings();
        
        var facialStyleComponents = avatarContext.Root
            .GetComponentsInChildren<StyleComponent>(true)
            .Where(x => x.ApplyToRenderer)
            .ToArray()
;
        var componentCount = facialStyleComponents.Length;
        if (componentCount == 0) return;

        if (componentCount > 1)
        {
            LocalizedLog.Warning("Log:warning:ApplyDefaultShapesPass:MultipleFacialStyleComponentWithApplyToRenderer");
        }

        var component = facialStyleComponents[0];

        //  除外されたブレンドシェイプを上書きせず、他は0で上書きする

        var set = new BlendShapeWeightSet();
        set.AddRange(avatarContext.FaceRenderer
            .GetBlendShapeWeights(avatarContext.FaceMesh)
            .Where(shape => !settings.ExcludedBlendShapeNames.Contains(shape.Name))
            .Select(shape => shape with { Weight = 0f }));
        ExpressionDataUtility.AddFirstFrameBlendShapes(component, set, avatarContext.BodyPath);

        var renderer = avatarContext.FaceRenderer;
        var mesh = avatarContext.FaceMesh;
        renderer.ApplyBlendShapes(mesh, set, -1); 
    }
}
