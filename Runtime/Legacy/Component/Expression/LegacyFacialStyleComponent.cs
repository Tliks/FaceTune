#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[DisallowMultipleComponent]
[Obsolete("Legacy serialized data retained only for migration.")]
internal class LegacyFacialStyleComponent : FaceTuneTagComponent
{
    public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();
    public bool ApplyToRenderer;
}

#pragma warning restore CS0618
