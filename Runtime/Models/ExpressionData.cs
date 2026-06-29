namespace Aoyon.FaceTune;

[Serializable]
internal class ExpressionData
{
    // AnimationClip
    public AnimationClip? Clip = null;
    public ClipImportOption ClipOption = ClipImportOption.NonZero;

    // Manual
    public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

    public bool AllBlendShapeAnimationAsFacial = false;

    public ExpressionData()
    {
    }
}

internal enum ClipImportOption
{
    All,
    NonZero,
    FacialStyleOverridesOrNonZero
}
