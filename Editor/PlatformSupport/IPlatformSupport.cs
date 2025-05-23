using nadena.dev.ndmf;

namespace com.aoyon.facetune.platform;

internal interface IPlatformSupport
{
    public bool IsTarget(Transform root);
    public void Initialize(Transform root) {}
    public SkinnedMeshRenderer? GetFaceRenderer();
    public void InstallPresets(BuildContext buildContext, SessionContext context, IEnumerable<Preset> presets)
    {
        return;
    }
    public IEnumerable<string> GetDisallowedBlendShape(SessionContext context)
    {
        return new string[] { };
    }
}