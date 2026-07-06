using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build;

[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
[DependsOnContext(typeof(VirtualControllerContext))]
internal class InstallBuildPass : FaceTunePass<InstallBuildPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.install-build";
    public override string DisplayName => "Install Build";

    protected override void Execute(FaceTuneContext context)
    {
        var settings = context.RequireSettings();
        var expressionProgram = context.RequireExpressionProgram();

        context.PlatformSupport.InstallBuild(context.BuildContext, settings, expressionProgram);
    }
}