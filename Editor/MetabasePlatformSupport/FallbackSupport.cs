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
        return FindRenderer("Face", StringComparison.OrdinalIgnoreCase)
               ?? FindRenderer("Body", StringComparison.Ordinal)
               ?? FindRenderer("body", StringComparison.Ordinal);
    }

    private SkinnedMeshRenderer? FindRenderer(string name, StringComparison comparison)
    {
        for (var i = 0; i < _root.childCount; i++)
        {
            var child = _root.GetChild(i);
            if (!string.Equals(child.name, name, comparison)) continue;
            if (child.TryGetComponent<SkinnedMeshRenderer>(out var renderer)) return renderer;
        }

        return null;
    }

    public ParameterDomainRegistry CreateBuiltInParameterDomains()
    {
        return new ParameterDomainRegistry();
    }

    public IEnumerable<string> GetExternallyControlledBlendShapeNames()
    {
        return Array.Empty<string>();;
    }

    public DnfCondition? ResolveHandGestureCondition(HandGestureCondition condition)
    {
        return null;
    }

    public DnfCondition? ResolveParameterCondition(ParameterCondition condition)
    {
        return null;
    }

    public string? ResolveGestureParameter(Hand hand) => null;

    public string? ResolveGestureWeightParameter(Hand hand) => null;
}
