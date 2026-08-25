namespace Aoyon.FaceTune.Build;

internal record struct BuildSettings(
    AvatarContext AvatarContext,
    ImmutableHashSet<string> ExternalEyeBlinkBlendShapeNames,
    ImmutableHashSet<string> ExternalLipSyncBlendShapeNames,
    ImmutableHashSet<string> ExplicitlyExcludedBlendShapeNames,
    bool AvoidEyeBlinkConflicts,
    bool AvoidLipSyncConflicts,
    ParameterDomainRegistry ParameterDomains)
{
    public bool IsBlendShapeExplicitlyExcluded(string name)
        => ExplicitlyExcludedBlendShapeNames.Contains(name);

    public BlendShapeWeight[] GetManagedZeroBlendShapes()
    {
        var excludedBlendShapeNames = ExplicitlyExcludedBlendShapeNames;
        return AvatarContext.FaceRenderer
            .GetBlendShapeWeights(AvatarContext.FaceMesh)
            .Where(shape => !excludedBlendShapeNames.Contains(shape.Name))
            .Select(shape => shape with { Weight = 0f })
            .ToArray();
    }
}
