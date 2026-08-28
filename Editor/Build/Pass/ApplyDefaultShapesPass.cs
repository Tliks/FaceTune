using Aoyon.FaceTune.Platforms;

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
        new FaceTuneResolver(avatarContext.Root).FacialData.AddRenderer(
            animations,
            avatarContext.BodyPath);
        animations.RemoveAll(animation =>
            !settings.CanWriteBlendShape(FaceTuneWriteKind.FacialData, animation.Name));
        if (animations.Count > 0)
        {
            set.AddRange(settings.GetManagedZeroBlendShapes());
            set.AddRange(animations.ToFirstFrameBlendShapes());
        }

        context.PlatformSupport.PostProcessDefaultBlendShapes(
            settings,
            context.RequireAvatarControlSettings(),
            set);
        set.RemoveRange(settings.ExplicitlyExcludedBlendShapeNames);
        set.RemoveRange(settings.FacialDataProhibitedBlendShapeNames);
        if (set.Count == 0) return;
        
        avatarContext.FaceRenderer.ApplyBlendShapes(avatarContext.FaceMesh, set, -1);
    }
}
