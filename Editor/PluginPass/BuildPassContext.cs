using nadena.dev.ndmf;
using com.aoyon.facetune.platform;

namespace com.aoyon.facetune.pass;

internal class BuildPassState
{
    public bool FTEnabled { get; } = false;
    public BuildPassContext? BuildPassContext { get; } = null;

    public BuildPassState()
    {
    }

    public BuildPassState(BuildContext buildContext)
    {
        var FTEnabled = SessionContextBuilder.TryBuild(buildContext.AvatarRootObject, out var sessionContext);
        if (!FTEnabled) return;

        var platformSupport = platform.PlatformSupport.GetSupport(buildContext.AvatarRootObject.transform);
        var dec = DefaultExpressionContextBuilder.BuildDefaultExpressionContext(sessionContext!);
        BuildPassContext = new BuildPassContext(buildContext, platformSupport, sessionContext!, dec);
    }

    public bool TryGetBuildPassContext([NotNullWhen(true)] out BuildPassContext? buildPassContext)
    {
        if (FTEnabled)
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