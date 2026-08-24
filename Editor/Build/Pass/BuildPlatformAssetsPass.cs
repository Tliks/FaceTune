using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build;

[DependsOnContext(typeof(VirtualControllerContext))]
internal sealed class BuildPlatformAssetsPass : FaceTunePass<BuildPlatformAssetsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.build-platform-assets";
    public override string DisplayName => "Build Platform Assets";

    protected override void Execute(FaceTuneContext context)
    {
        var backend = context.PlatformSupport.BuildBackend;
        if (backend == null) return;

        backend.Build(
            context.BuildContext,
            context.RequireSettings(),
            context.RequireAvatarControlSettings(),
            context.RequireExpressionPlan(),
            context.RequireMenuPlan(),
            context.RequireParameterPlan());
    }
}
