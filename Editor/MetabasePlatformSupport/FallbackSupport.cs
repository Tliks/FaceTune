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
        return _root.FindDirectChildComponent<SkinnedMeshRenderer>("Face", StringComparison.OrdinalIgnoreCase).DestroyedAsNull()
               ?? _root.FindDirectChildComponent<SkinnedMeshRenderer>("Body", StringComparison.Ordinal).DestroyedAsNull()
               ?? _root.FindDirectChildComponent<SkinnedMeshRenderer>("body", StringComparison.Ordinal).DestroyedAsNull()
               ?? _root.GetComponentInChildren<SkinnedMeshRenderer>(true).DestroyedAsNull();
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
