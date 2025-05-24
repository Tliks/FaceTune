using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;

namespace com.aoyon.facetune.platform;

internal interface IPlatformSupport
{
    public bool IsTarget(Transform root);
    public void Initialize(Transform root)
    {
        return;
    }
    public SkinnedMeshRenderer? GetFaceRenderer();
    public void InstallPresets(BuildContext buildContext, SessionContext context, IEnumerable<Preset> presets)
    {
        return;
    }
    public IEnumerable<string> GetTrackedBlendShape(SessionContext context)
    {
        return new string[] { };
    }
    public string AssignParameterName(ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        return string.Empty;
    }
}