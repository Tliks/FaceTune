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
}
