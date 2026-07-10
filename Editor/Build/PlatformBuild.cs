using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

internal interface IPlatformBuildBackend
{
    void Emit(
        BuildContext buildContext,
        BuildSettings settings,
        ExpressionProgram expressions,
        MenuProgram menus);

    void Finalize(FaceTuneContext context);
}
