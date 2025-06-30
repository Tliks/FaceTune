namespace aoyon.facetune.platform;

internal class FallbackSupport : IPlatformSupport
{     
    public bool IsTarget(Transform root)
    {
        return true;
    }

    private Transform _root = null!;
    public void Initialize(Transform root)
    {
        _root = root;
    }

    public SkinnedMeshRenderer? GetFaceRenderer()
    {
        SkinnedMeshRenderer? faceRenderer = null;
        for (int i = 0; i < _root.childCount; i++)
        {
            var child = _root.GetChild(i);
            if (child.name == "Body")
            {
                faceRenderer = child.GetComponentNullable<SkinnedMeshRenderer>();
                if (faceRenderer != null) { break; }
            }
        }
        return faceRenderer;
    }
}