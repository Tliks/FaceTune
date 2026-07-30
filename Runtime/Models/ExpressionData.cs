namespace Aoyon.FaceTune;

[Serializable]
internal class ExpressionData
{
    // AnimationClip
    public AnimationClip? Clip = null;
    public ClipImportOption ClipOption = ClipImportOption.NonZero;

    // Manual
    public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

    public ExpressionData()
    {
    }
}

internal enum ClipImportOption
{
    All,
    NonZero
}
