using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class VRChatBuildBackend : IPlatformBuildBackend
{
    public static VRChatBuildBackend Instance { get; } = new();

    private VRChatBuildBackend()
    {
    }

    public void Emit(
        BuildContext buildContext,
        BuildSettings settings,
        ExpressionProgram expressions,
        MenuProgram menus)
    {
        using (new Utils.ProfilingSampleScope("FaceTune.Emit.VRChat.Menu"))
        {
            VRChatMenuBuilder.Emit(buildContext, menus);
        }

        using (new Utils.ProfilingSampleScope("FaceTune.Emit.VRChat.Parameters"))
        {
            VRChatParameterBuilder.Emit(buildContext, menus);
        }

        using (new Utils.ProfilingSampleScope("FaceTune.Emit.VRChat.Animator"))
        {
            VRChatAnimatorBuilder.Emit(buildContext, settings, expressions);
        }
    }

    public void Finalize(FaceTuneContext context)
    {
        VRChatMenuBuilder.Finalize(context);
    }
}
