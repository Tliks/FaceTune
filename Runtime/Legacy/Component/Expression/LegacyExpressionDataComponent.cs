#pragma warning disable CS0618

namespace Aoyon.FaceTune
{
    [Obsolete("Legacy serialized data retained only for migration.")]
    internal class LegacyExpressionDataComponent : LegacyFaceTuneTagComponent
    {
        public AnimationClip? Clip;
        public LegacyClipImportOption ClipOption = LegacyClipImportOption.NonZero;
        public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();
        public bool AllBlendShapeAnimationAsFacial;
    }
}

#pragma warning restore CS0618
