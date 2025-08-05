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
        Enabled = SessionContextBuilder.TryBuild(root, out var sessionContext, out var result);
        if (!Enabled) return;

        var platformSupport = Platforms.MetabasePlatformSupport.GetSupport(root.transform);
        BuildPassContext = new BuildPassContext(sessionContext!, platformSupport);
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
    public SessionContext SessionContext { get; }
    public IMetabasePlatformSupport PlatformSupport { get; }

    public BuildPassContext(SessionContext sessionContext, IMetabasePlatformSupport platformSupport)
    {
        SessionContext = sessionContext;
        PlatformSupport = platformSupport;
    }
}