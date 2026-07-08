using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal sealed class AnimatorInstaller : InstallerBase
{
    private readonly AnimatorBuildPlan _plan;

    public AnimatorInstaller(
        VirtualAnimatorController virtualController,
        AvatarContext avatarContext,
        bool useWriteDefaults,
        IAnimatorPlatformServices platformServices,
        AnimatorBuildPlan plan) : base(virtualController, avatarContext, useWriteDefaults, platformServices)
    {
        _plan = plan;
    }

    public void Execute()
    {
        // Plan installation is intentionally not implemented yet.
        // This class consumes an already-built AnimatorBuildPlan; plan construction belongs to platform support.
    }
}
