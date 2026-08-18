using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

internal interface IPlatformBuildBackend
{
    void Build(
        BuildContext buildContext,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ExpressionPlan expressions,
        MenuPlan menus,
        ParameterPlan parameters);

    void Finish(FaceTuneContext context);
}
