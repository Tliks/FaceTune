namespace Aoyon.FaceTune.Build;

internal class ApplyDefaultShapesPass : FaceTunePass<ApplyDefaultShapesPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.apply-default-shapes";
    public override string DisplayName => "Apply Default Shapes";

    protected override void Execute(FaceTuneContext context)
    {
        var avatarContext = context.AvatarContext;
        var settings = context.RequireSettings();

        var set = new BlendShapeWeightSet();

        var animations = new List<BlendShapeWeightAnimation>();
        new FacialAnimationResolver(avatarContext.Root).AddRenderer(animations);
        animations.RemoveAll(animation =>
            !settings.CanWriteBlendShape(FaceTuneWriteKind.FacialData, animation.Name));
        if (animations.Count > 0)
        {
            set.AddRange(settings.GetManagedBlendShapeNames(FaceTuneWriteKind.FacialData)
                .Select(name => new BlendShapeWeight(name, 0f)));
            set.AddRange(animations.ToFirstFrameBlendShapes());
        }

        context.PlatformSupport.PostProcessDefaultBlendShapes(
            settings,
            context.RequireAvatarControlSettings(),
            set);
        set.RemoveRange(settings.FacialDataProhibitedBlendShapeNames);
        if (set.Count == 0) return;

        var apply = new BlendShapeApply(
            new ImmutableBlendShapeWeightSet(set),
            IgnoredNames: settings.ExplicitlyExcludedBlendShapeNames);
        avatarContext.FaceRenderer.ApplyBlendShapes(apply, avatarContext.FaceMesh);
    }
}
