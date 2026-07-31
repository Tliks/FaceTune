namespace Aoyon.FaceTune;

[Serializable]
internal class ExpressionData
{
    public AnimationClip? Clip = null;
    public ClipImportOption ClipOption = ClipImportOption.NonZero;

    public AvatarObjectReference DataReference = new();

    public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

    public ExpressionData()
    {
    }

    public void ResolveReferences(Component owner)
    {
        DataReference.Get(owner);
    }
}

internal enum ClipImportOption
{
    All,
    NonZero
}
