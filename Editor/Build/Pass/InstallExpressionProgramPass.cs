using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build;

[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
[DependsOnContext(typeof(VirtualControllerContext))]
internal class InstallExpressionProgramPass : Pass<InstallExpressionProgramPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.install-expression-program";
    public override string DisplayName => "Install Expression Program";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;

        var expressionProgram = context.GetState<ExpressionProgram>();

        Profiler.BeginSample("InstallExpressionProgram");
        buildPassContext.PlatformSupport.InstallExpressionProgram(buildPassContext, context, expressionProgram);
        Profiler.EndSample();
    }
}