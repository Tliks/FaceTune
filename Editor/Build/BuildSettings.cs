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

    public IEnumerable<BlendShapeWeight> GetManagedBlendShapes(FaceTuneWriteKind writeKind)
        => EnumerateBlendShapeWeights(GetProhibitedBlendShapeNames(writeKind));

    public IEnumerable<BlendShapeWeight> GetManagedBlendShapesForAnyWriteKind()
        => EnumerateBlendShapeWeights(GetProhibitedForAnyWriteKind());

    public IEnumerable<string> GetManagedBlendShapeNames(FaceTuneWriteKind writeKind)
        => EnumerateBlendShapes(GetProhibitedBlendShapeNames(writeKind))
            .Select(entry => entry.Name);

    public IEnumerable<string> GetManagedBlendShapeNamesForAnyWriteKind()
        => EnumerateBlendShapes(GetProhibitedForAnyWriteKind())
            .Select(entry => entry.Name);

    private ImmutableHashSet<string> GetProhibitedForAnyWriteKind()
        => FacialDataProhibitedBlendShapeNames
            .Intersect(EyeBlinkAnimationProhibitedBlendShapeNames)
            .Intersect(LipSyncAnimationProhibitedBlendShapeNames);

    private IEnumerable<BlendShapeWeight> EnumerateBlendShapeWeights(
        ImmutableHashSet<string> prohibited)
    {
        var renderer = AvatarContext.FaceRenderer;
        foreach (var (index, name) in EnumerateBlendShapes(prohibited))
            yield return new BlendShapeWeight(name, renderer.GetBlendShapeWeight(index));
    }

    private IEnumerable<(int Index, string Name)> EnumerateBlendShapes(
        ImmutableHashSet<string> prohibited)
    {
        var mesh = AvatarContext.FaceMesh;
        var explicitlyExcluded = ExplicitlyExcludedBlendShapeNames;
        for (var index = 0; index < mesh.blendShapeCount; index++)
        {
            var name = mesh.GetBlendShapeName(index);
            if (!explicitlyExcluded.Contains(name) && !prohibited.Contains(name))
                yield return (index, name);
        }
    }
}
