using nadena.dev.ndmf;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal class BuildPassState
{
    public bool Enabled { get; }
    public BuildPassContext? BuildPassContext { get; }

    public BuildPassState()
    {
        throw new Exception("BuildPassState is not initialized");
    }

    public BuildPassState(BuildContext buildContext) : this(buildContext.AvatarRootObject)
    {
    }

    public BuildPassState(GameObject root)
    {
        var canBuild = AvatarContextBuilder.TryBuild(root, out var avatarContext, out var result);
        var anyComponents = root.GetComponentsInChildren<FaceTuneTagComponent>(true).Count() > 0;
        Enabled = canBuild && anyComponents;
        if (!Enabled) return;

        var platformSupport = Platforms.MetabasePlatformSupport.GetSupport(root.transform);
        BuildPassContext = new BuildPassContext(avatarContext!, platformSupport);
    }

    public bool TryGetBuildPassContext([NotNullWhen(true)] out BuildPassContext? buildPassContext)
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

internal class BuildPassContext
{
    public AvatarContext AvatarContext { get; }
    public IMetabasePlatformSupport PlatformSupport { get; }

    public BuildPassContext(AvatarContext avatarContext, IMetabasePlatformSupport platformSupport)
    {
        AvatarContext = avatarContext;
        PlatformSupport = platformSupport;
    }
}