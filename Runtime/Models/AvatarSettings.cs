
namespace Aoyon.FaceTune;

[Serializable]
internal class AvatarSettings
{
    public AvatarObjectReference FaceObjectReference = new();
    public List<string> ExcludedBlendShapeNames = new();
    public bool AvoidEyeBlinkConflicts = true;
    public bool AvoidLipSyncConflicts = true;

    public void ResolveReferences(Component owner)
    {
        FaceObjectReference.Get(owner);
    }
}