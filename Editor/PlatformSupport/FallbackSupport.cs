using nadena.dev.ndmf;
namespace com.aoyon.facetune.platform;

internal class FallbackSupport : IPlatformSupport
{     
    public bool IsTarget(Transform root)
    {
        return true;
    }

    public SkinnedMeshRenderer? GetFaceRenderer(Transform root)
    {
        SkinnedMeshRenderer? faceRenderer = null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == "Body")
            {
                faceRenderer = child.GetComponentNullable<SkinnedMeshRenderer>();
                if (faceRenderer != null) { break; }
            }
        }
        return faceRenderer;
    }

    public void InstallPresets(BuildContext buildContext, SessionContext context, List<Preset> presets)
    {
        return;
    }
}