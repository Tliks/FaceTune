namespace Aoyon.FaceTune.Platforms;

internal sealed class FallbackSupport : IMetabasePlatformSupport
{
    private readonly Transform _root;

    public FallbackSupport(Transform root)
    {
        _root = root;
    }

    public SkinnedMeshRenderer? GetFaceRenderer()
    {
        for (var i = 0; i < _root.childCount; i++)
        {
            var child = _root.GetChild(i);
            if (child.name != "Body") continue;
            if (child.TryGetComponent<SkinnedMeshRenderer>(out var renderer)) return renderer;
        }

        return null;
    }
}
