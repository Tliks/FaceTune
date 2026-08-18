using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class VRChatBuildBackend : IPlatformBuildBackend
{
    public static VRChatBuildBackend Instance { get; } = new();

    private VRChatBuildBackend()
    {
    }

    public void Build(
        BuildContext buildContext,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ExpressionPlan expressions,
        MenuPlan menus,
        ParameterPlan parameters)
    {
        using (new Utils.ProfilingSampleScope("FaceTune.Build.VRChat.Menu"))
        {
            VRChatMenuBuilder.Build(buildContext, menus);
        }

        using (new Utils.ProfilingSampleScope("FaceTune.Build.VRChat.Parameters"))
        {
            VRChatParameterBuilder.Build(buildContext, parameters);
        }

        using (new Utils.ProfilingSampleScope("FaceTune.Build.VRChat.Animator"))
        {
            VRChatAnimatorBuilder.Build(buildContext, settings, avatarControlSettings, expressions);
        }
    }

    public void Finish(FaceTuneContext context)
    {
        VRChatMenuBuilder.Finish(context);
    }
}
