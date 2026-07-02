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
        
        var canBuild = AvatarContext.TryGet(root, out var avatarContext, out _);
        var anyComponents = root.GetComponentsInChildren<FaceTuneTagComponent>(true).Any();
        Enabled = canBuild && anyComponents;
        if (!Enabled) return;

        var platformSupport = MetabasePlatformSupport.GetSupport(root.transform);
        BuildPassContext = new FaceTuneContext(buildContext, avatarContext!, platformSupport);
    }

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

internal record struct FaceTuneContext(BuildContext BuildContext, AvatarContext AvatarContext, IMetabasePlatformSupport PlatformSupport);

internal abstract class FaceTunePass<TPass> : Pass<TPass> where TPass : Pass<TPass>, new()
{
    protected sealed override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;
        Execute(buildPassContext.Value);
    }

    protected abstract void Execute(FaceTuneContext context);
}
