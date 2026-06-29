namespace Aoyon.FaceTune
{
    [AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
    internal class DataComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Data";

        public ExpressionData Data = new();

        // AnimationClip
        [Obsolete] public AnimationClip? Clip = null;
        [Obsolete] public ClipImportOption ClipOption = ClipImportOption.NonZero;

        // Manual
        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        [Obsolete] public bool AllBlendShapeAnimationAsFacial = false;

    }
}