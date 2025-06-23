using nadena.dev.ndmf;
using com.aoyon.facetune.platform;

namespace com.aoyon.facetune.pass;

internal class BuildPassState
{
    public BuildContext BuildContext { get; private set; } = null!;
    public IPlatformSupport PlatformSupport { get; private set; } = null!;
    public SessionContext? SessionContext { get; private set; }

    public static BuildPassState? Get(BuildContext context)
    {
        var state = new BuildPassState();
        state.BuildContext = context;
        var root = context.AvatarRootObject.transform;
        state.PlatformSupport = platform.PlatformSupport.GetSupport(root);
        state.PlatformSupport.Initialize(root);
        if (SessionContextBuilder.TryBuild(context.AvatarRootObject, out var sessionContext))
        {
            state.SessionContext = sessionContext;
        }
        return state;
    }
}