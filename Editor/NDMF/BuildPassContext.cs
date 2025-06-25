using nadena.dev.ndmf;
using com.aoyon.facetune.platform;

namespace com.aoyon.facetune.ndmf;

internal class BuildPassState
{
    public bool Enabled { get; }
    public BuildPassContext? BuildPassContext { get; }

    public BuildPassState()
    {
        throw new Exception("BuildPassState is not initialized");
    }

    public BuildPassState(BuildContext buildContext)
    {
        Enabled = SessionContextBuilder.TryBuild(buildContext.AvatarRootObject, out var sessionContext);
        if (!Enabled) return;

        var platformSupport = platform.PlatformSupport.GetSupport(buildContext.AvatarRootObject.transform);
        var dec = DefaultExpressionContextBuilder.BuildDefaultExpressionContext(sessionContext!);
        BuildPassContext = new BuildPassContext(buildContext, platformSupport, sessionContext!, dec);
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
    public BuildContext BuildContext { get; }
    public IPlatformSupport PlatformSupport { get; }
    public SessionContext SessionContext { get; }
    public DefaultExpressionContext DEC { get; }

    public BuildPassContext(BuildContext buildContext, IPlatformSupport platformSupport, SessionContext sessionContext, DefaultExpressionContext dec)
    {
        BuildContext = buildContext;
        PlatformSupport = platformSupport;
        SessionContext = sessionContext;
        DEC = dec;
    }
}