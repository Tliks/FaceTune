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

    public bool IsBlendShapeProhibited(FaceTuneWriteKind writeKind, string name)
        => GetProhibitedBlendShapeNames(writeKind).Contains(name);

    private ImmutableHashSet<string> GetProhibitedBlendShapeNames(FaceTuneWriteKind writeKind)
        => writeKind switch
        {
            FaceTuneWriteKind.FacialData => FacialDataProhibitedBlendShapeNames,
            FaceTuneWriteKind.EyeBlinkAnimation => EyeBlinkAnimationProhibitedBlendShapeNames,
            FaceTuneWriteKind.LipSyncAnimation => LipSyncAnimationProhibitedBlendShapeNames,
            _ => throw new ArgumentOutOfRangeException(nameof(writeKind), writeKind, null)
        };

    public bool CanWriteBlendShape(
        FaceTuneWriteKind writeKind,
        string name)
    {
        return !IsBlendShapeExplicitlyExcluded(name)
               && !IsBlendShapeProhibited(writeKind, name);
    }

    public IEnumerable<BlendShapeWeight> GetManagedBlendShapesForAnyWriteKind()
    {
        var prohibited = FacialDataProhibitedBlendShapeNames
            .Intersect(EyeBlinkAnimationProhibitedBlendShapeNames)
            .Intersect(LipSyncAnimationProhibitedBlendShapeNames);
        return EnumerateManagedBlendShapes(prohibited, false);
    }

    public IEnumerable<BlendShapeWeight> GetManagedZeroBlendShapes(FaceTuneWriteKind writeKind)
        => EnumerateManagedBlendShapes(GetProhibitedBlendShapeNames(writeKind), true);

    private IEnumerable<BlendShapeWeight> EnumerateManagedBlendShapes(
        ImmutableHashSet<string> prohibited,
        bool zero)
    {
        var avatarContext = AvatarContext;
        var explicitlyExcluded = ExplicitlyExcludedBlendShapeNames;
        var mesh = avatarContext.FaceMesh;
        var renderer = avatarContext.FaceRenderer;
        for (var index = 0; index < mesh.blendShapeCount; index++)
        {
            var name = mesh.GetBlendShapeName(index);
            if (explicitlyExcluded.Contains(name) || prohibited.Contains(name)) continue;

            yield return new BlendShapeWeight(
                name,
                zero ? 0f : renderer.GetBlendShapeWeight(index));
        }
    }
}
