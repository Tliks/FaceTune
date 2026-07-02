using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build;

[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
[DependsOnContext(typeof(VirtualControllerContext))]
internal class InstallExpressionProgramPass : FaceTunePass<InstallExpressionProgramPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.install-expression-program";
    public override string DisplayName => "Install Expression Program";

    protected override void Execute(FaceTuneContext context)
    {
        var expressionProgram = context.RequireExpressionProgram();

        Profiler.BeginSample("InstallExpressionProgram");
        context.PlatformSupport.InstallExpressionProgram(context, context.BuildContext, expressionProgram);
        Profiler.EndSample();
    }
}