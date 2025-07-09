using nadena.dev.ndmf;
using aoyon.facetune.platform;

namespace aoyon.facetune.build;

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

        var platformSupport = platform.PlatformSupport.GetSupport(root.transform);
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
    public IPlatformSupport PlatformSupport { get; }

    public BuildPassContext(SessionContext sessionContext, IPlatformSupport platformSupport)
    {
        SessionContext = sessionContext;
        PlatformSupport = platformSupport;
    }
}