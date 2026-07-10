namespace Aoyon.FaceTune.Build;

[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
internal sealed class FinalizePlatformBuildPass : FaceTunePass<FinalizePlatformBuildPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.finalize-platform-build";
    public override string DisplayName => "Finalize Platform Build";

    protected override void Execute(FaceTuneContext context)
    {
        context.PlatformSupport.BuildBackend?.Finalize(context);
    }
}
