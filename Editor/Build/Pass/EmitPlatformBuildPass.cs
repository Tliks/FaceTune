using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build;

[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
[DependsOnContext(typeof(VirtualControllerContext))]
internal sealed class EmitPlatformBuildPass : FaceTunePass<EmitPlatformBuildPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.emit-platform-build";
    public override string DisplayName => "Emit Platform Build";

    protected override void Execute(FaceTuneContext context)
    {
        var backend = context.PlatformSupport.BuildBackend;
        if (backend == null) return;

        backend.Emit(
            context.BuildContext,
            context.RequireSettings(),
            context.RequireExpressionProgram(),
            context.RequireMenuProgram());
    }
}
