using nadena.dev.ndmf;

namespace com.aoyon.facetune.platform;

internal interface IPlatformSupport
{
    public bool IsTarget(Transform root);
    public SkinnedMeshRenderer? GetFaceRenderer(Transform root);
    public void InstallPresets(BuildContext buildContext, SessionContext context, List<Preset> presets);
}