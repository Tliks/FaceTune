namespace Aoyon.FaceTune.Build;

internal record struct BuildSettings(
    AvatarContext AvatarContext,
    ImmutableHashSet<string> ExternallyControlledBlendShapeNames,
    ImmutableHashSet<string> ExplicitlyExcludedBlendShapeNames,
    bool AvoidEyeBlinkConflicts,
    bool AvoidLipSyncConflicts,
    ParameterDomainRegistry ParameterDomains)
{
    public bool IsBlendShapeExcluded(string name)
        => ExternallyControlledBlendShapeNames.Contains(name)
           || ExplicitlyExcludedBlendShapeNames.Contains(name);
}
