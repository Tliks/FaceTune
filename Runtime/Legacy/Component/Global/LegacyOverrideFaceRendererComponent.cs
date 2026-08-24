#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[DisallowMultipleComponent]
[Obsolete("Legacy serialized data retained only for migration.")]
internal class LegacyOverrideFaceRendererComponent : FaceTuneTagComponent
{
    [SerializeField] internal AvatarObjectReference m_faceObjectReference = new();
}

#pragma warning restore CS0618
