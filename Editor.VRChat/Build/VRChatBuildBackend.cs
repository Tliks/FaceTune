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
        VRChatMenuBuilder.Emit(buildContext, menus);
        VRChatParameterBuilder.Emit(buildContext, menus);
        VRChatAnimatorBuilder.Emit(buildContext, settings, expressions);
    }

    public void Finalize(FaceTuneContext context)
    {
        VRChatMenuBuilder.Finalize(context);
    }
}
