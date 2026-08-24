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
               ?? FindRenderer("body", StringComparison.Ordinal)
               ?? _root.GetComponentInChildren<SkinnedMeshRenderer>(true).DestroyedAsNull();
    }

    private SkinnedMeshRenderer? FindRenderer(string name, StringComparison comparison)
    {
        for (var i = 0; i < _root.childCount; i++)
        {
            var child = _root.GetChild(i);
            if (!string.Equals(child.name, name, comparison)) continue;
            if (child.TryGetComponent<SkinnedMeshRenderer>(out var renderer))
            {
                return renderer.DestroyedAsNull();
            }
        }

        return null;
    }

    public ParameterDomainRegistry CreateBuiltInParameterDomains()
    {
        return ParameterDomainRegistry.Empty;
    }

    public IEnumerable<string> GetExternalEyeBlinkBlendShapeNames()
        => Array.Empty<string>();

    public IEnumerable<string> GetExternalLipSyncBlendShapeNames()
        => Array.Empty<string>();

    public DnfCondition? ResolveHandGestureCondition(
        HandGestureCondition condition,
        ParameterDomainRegistry parameterDomains)
    {
        return null;
    }

    public DnfCondition? ResolveParameterCondition(
        ParameterCondition condition,
        ParameterDomainRegistry parameterDomains)
    {
        return null;
    }

    public string? ResolveGestureParameter(Hand hand) => null;

    public string? ResolveGestureWeightParameter(Hand hand) => null;
}
