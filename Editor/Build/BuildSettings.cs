namespace Aoyon.FaceTune.Build;

internal record struct BuildSettings(
    AvatarContext AvatarContext,
    IReadOnlyCollection<string> ExcludedBlendShapeNames,
    bool AvoidEyeBlinkConflicts,
    bool AvoidLipSyncConflicts,
    ParameterDomainRegistry ParameterDomains);
