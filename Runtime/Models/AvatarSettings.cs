
namespace Aoyon.FaceTune;

internal enum FaceMeshSelectionMode
{
    Automatic,
    Manual
}

[Serializable]
internal class AvatarSettings
{
    public FaceMeshSelectionMode FaceMeshSelection = DefaultFaceMeshSelection;
    public AvatarObjectReference FaceObjectReference;
    public List<string> ExcludedBlendShapeNames;
    public bool AvoidEyeBlinkConflicts;
    public bool AvoidLipSyncConflicts;

    public const FaceMeshSelectionMode DefaultFaceMeshSelection = FaceMeshSelectionMode.Automatic;
    public const bool DefaultAvoidEyeBlinkConflicts = true;
    public const bool DefaultAvoidLipSyncConflicts = true;

    public AvatarSettings()
    {
        FaceObjectReference = CreateDefaultFaceObjectReference();
        ExcludedBlendShapeNames = CreateDefaultExcludedBlendShapeNames();
        AvoidEyeBlinkConflicts = DefaultAvoidEyeBlinkConflicts;
        AvoidLipSyncConflicts = DefaultAvoidLipSyncConflicts;
    }

    public static AvatarSettings Default => new();

    internal static AvatarObjectReference CreateDefaultFaceObjectReference() => new();
    internal static List<string> CreateDefaultExcludedBlendShapeNames() => new();

    public void ResolveReferences(Component owner)
    {
        FaceObjectReference.Get(owner);
    }
}