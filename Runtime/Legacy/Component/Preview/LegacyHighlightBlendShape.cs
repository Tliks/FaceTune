#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[Obsolete("Legacy serialized data retained only for migration.")]
internal class LegacyHilightBlendShape : LegacyFaceTuneTagComponent
{
    public Mesh Mesh = null!;
    public Vector3 Position;
    public Color highlightColor = Color.red;
}

#pragma warning restore CS0618
