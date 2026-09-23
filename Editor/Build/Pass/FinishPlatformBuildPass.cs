using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build;

[DependsOnContext(typeof(VirtualControllerContext))]
internal sealed class FinishPlatformBuildPass : FaceTunePass<FinishPlatformBuildPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.finish-platform-build";
    public override string DisplayName => "Finish Platform Build";

    protected override void Execute(FaceTuneContext context)
    {
        context.PlatformSupport.BuildBackend?.Finish(context);
    }
}
