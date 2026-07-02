using nadena.dev.ndmf;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal class BuildPassState
{
    public bool Enabled { get; }
    public FaceTuneContext? BuildPassContext { get; }

    public BuildPassState()
    {
        throw new Exception("BuildPassState is not initialized");
    }

    public BuildPassState(BuildContext buildContext)
    {
        var root = buildContext.AvatarRootObject;
        
        var canBuild = AvatarContext.TryGet(root, out var avatarContext, out var result);
        var anyComponents = root.GetComponentsInChildren<FaceTuneTagComponent>(true).Count() > 0;
        Enabled = canBuild && anyComponents;
        if (!Enabled) return;

        var platformSupport = MetabasePlatformSupport.GetSupport(root.transform);
        BuildPassContext = new FaceTuneContext(buildContext, avatarContext!, platformSupport);
    }
}

internal record struct FaceTuneContext(BuildContext BuildContext, AvatarContext AvatarContext, IMetabasePlatformSupport PlatformSupport);

internal class FaceTunePass : Pass<FaceTunePass>
{
    protected override void Execute(BuildContext context)
    {
        context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext);
    }

    protected abstract void Execute(FaceTuneContext context);

    public bool TryGetBuildPassContext([NotNullWhen(true)] out FaceTuneContext? buildPassContext)
    {
        if (Enabled)
        {
            buildPassContext = BuildPassContext!;
            return true;
        }
        buildPassContext = null;
        return false;
    }
}