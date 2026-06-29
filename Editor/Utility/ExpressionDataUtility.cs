namespace Aoyon.FaceTune;

internal static class ExpressionDataUtility
{
    public static void ResolveBlendShapes(
        DataComponent component,
        ICollection<BlendShapeWeight> resultToAdd,
        IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
        string bodyPath)
    {
        ResolveBlendShapes(component.Data, resultToAdd, facialAnimations, bodyPath);
    }

    public static void ResolveBlendShapes(
        ExpressionData data,
        ICollection<BlendShapeWeight> resultToAdd,
        IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
        string bodyPath)
    {
        if (data.Clip != null)
        {
            var facialPath = data.AllBlendShapeAnimationAsFacial ? null : bodyPath;
            data.Clip.GetFirstFrameBlendShapes(data.ClipOption, resultToAdd, facialPath, facialAnimations);
        }

        foreach (var animation in data.BlendShapeAnimations)
        {
            resultToAdd.Add(animation.ToFirstFrameBlendShape());
        }
    }

    public static void ResolveAnimations(
        ExpressionData data,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        AvatarContext avatarContext)
    {
        ResolveAnimations(data, resultToAdd, Array.Empty<BlendShapeWeightAnimation>(), avatarContext.BodyPath);
    }

    public static void ResolveAnimations(
        DataComponent component,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        AvatarContext avatarContext)
    {
        ResolveAnimations(component, resultToAdd, Array.Empty<BlendShapeWeightAnimation>(), avatarContext.BodyPath);
    }

    public static void ResolveAnimations(
        DataComponent component,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
        string bodyPath)
    {
        ResolveAnimations(component.Data, resultToAdd, facialAnimations, bodyPath);
    }

    public static void ResolveAnimations(
        ExpressionData data,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        IReadOnlyList<BlendShapeWeightAnimation> facialAnimations,
        string bodyPath)
    {
        if (data.Clip != null)
        {
            var facialPath = data.AllBlendShapeAnimationAsFacial ? null : bodyPath;
            data.Clip.GetBlendShapeAnimations(data.ClipOption, resultToAdd, facialPath, facialAnimations);
        }

        foreach (var animation in data.BlendShapeAnimations)
        {
            resultToAdd.Add(animation);
        }
    }

    public static void ResolveStyleBlendShapes(StyleComponent component, ICollection<BlendShapeWeight> resultToAdd)
    {
        ResolveBlendShapes(component.Data, resultToAdd, Array.Empty<BlendShapeWeightAnimation>(), string.Empty);
    }

    public static void ResolveStyleAnimations(StyleComponent component, ICollection<BlendShapeWeightAnimation> resultToAdd)
    {
        ResolveAnimations(component.Data, resultToAdd, Array.Empty<BlendShapeWeightAnimation>(), string.Empty);
    }
}
