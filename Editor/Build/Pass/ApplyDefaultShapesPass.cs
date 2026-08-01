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
            .ToArray();
        if (facialStyleComponents.Length > 1)
        {
            LocalizedLog.Warning("log.applyDefaultShapesPass.multipleFacialStyleComponentWithApplyToRenderer.warning");
        }

        var set = new BlendShapeWeightSet();
        var component = facialStyleComponents.FirstOrDefault();
        if (component != null)
        {
            set.AddRange(avatarContext.FaceRenderer
                .GetBlendShapeWeights(avatarContext.FaceMesh)
                .Where(shape => !settings.ExcludedBlendShapeNames.Contains(shape.Name))
                .Select(shape => shape with { Weight = 0f }));

            var animations = new List<BlendShapeWeightAnimation>();
            component.GetAnimations(animations, avatarContext.BodyPath);
            set.AddRange(animations.ToFirstFrameBlendShapes());
        }

        context.PlatformSupport.PostProcessDefaultBlendShapes(settings, set);
        if (set.Count == 0) return;
        avatarContext.FaceRenderer.ApplyBlendShapes(avatarContext.FaceMesh, set, -1);
    }
}
