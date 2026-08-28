using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal record struct BuildSettings(
    AvatarContext AvatarContext,
    ImmutableHashSet<string> FacialDataProhibitedBlendShapeNames,
    ImmutableHashSet<string> EyeBlinkAnimationProhibitedBlendShapeNames,
    ImmutableHashSet<string> LipSyncAnimationProhibitedBlendShapeNames,
    ImmutableHashSet<string> ExplicitlyExcludedBlendShapeNames,
    bool AvoidEyeBlinkConflicts,
    bool AvoidLipSyncConflicts,
    ParameterDomainRegistry ParameterDomains)
{
    public bool IsBlendShapeExplicitlyExcluded(string name)
        => ExplicitlyExcludedBlendShapeNames.Contains(name);

    public bool IsBlendShapeProhibited(
        FaceTuneWriteKind writeKind,
        string name)
    {
        var prohibitedNames = writeKind switch
        {
            FaceTuneWriteKind.FacialData => FacialDataProhibitedBlendShapeNames,
            FaceTuneWriteKind.EyeBlinkAnimation => EyeBlinkAnimationProhibitedBlendShapeNames,
            FaceTuneWriteKind.LipSyncAnimation => LipSyncAnimationProhibitedBlendShapeNames,
            _ => throw new ArgumentOutOfRangeException(nameof(writeKind), writeKind, null)
        };
        return prohibitedNames.Contains(name);
    }

    public bool CanWriteBlendShape(
        FaceTuneWriteKind writeKind,
        string name)
    {
        return !IsBlendShapeExplicitlyExcluded(name)
               && !IsBlendShapeProhibited(writeKind, name);
    }

    public BlendShapeWeight[] GetManagedZeroBlendShapes()
    {
        var avatarContext = AvatarContext;
        var explicitlyExcluded = ExplicitlyExcludedBlendShapeNames;
        var prohibited = FacialDataProhibitedBlendShapeNames;
        return avatarContext.FaceRenderer
            .GetBlendShapeWeights(avatarContext.FaceMesh)
            .Where(shape => !explicitlyExcluded.Contains(shape.Name)
                && !prohibited.Contains(shape.Name))
            .Select(shape => shape with { Weight = 0f })
            .ToArray();
    }
}
