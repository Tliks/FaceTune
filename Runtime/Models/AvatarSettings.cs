using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune;

[Serializable]
internal class AvatarSettings
{
    public AvatarObjectReference FaceObjectReference;
    public List<string> ExcludedBlendShapeNames;
    public float DurationSeconds;
    public bool ParmaterCompression;
    public bool SupressTrackingControl;

    public AvatarSettings()
    {
        FaceObjectReference = new();
        ExcludedBlendShapeNames = new();
        DurationSeconds = 0.1f;
        ParmaterCompression = false;
        SupressTrackingControl = false;
    }

    public static AvatarSettings Default => new();

    public void ResolveReferences(Component owner)
    {
        FaceObjectReference.Get(owner);
    }
}