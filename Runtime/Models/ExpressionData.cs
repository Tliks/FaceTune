namespace Aoyon.FaceTune;

[Serializable]
internal class ExpressionData
{
    public AnimationClip? Clip = null;
    public ClipImportOption ClipOption = ClipImportOption.NonZero;

    public Transform? DataReference = null;

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
