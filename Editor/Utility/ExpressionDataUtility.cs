namespace Aoyon.FaceTune;

internal static class ExpressionDataUtility
{
    public static void AddFirstFrameBlendShapes(
        ExpressionData data,
        ICollection<BlendShapeWeight> resultToAdd,
        string bodyPath,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null)
    {
        facialAnimations ??= Array.Empty<BlendShapeWeightAnimation>();

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

    public static void AddAnimations(
        ExpressionData data,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        string bodyPath,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null)
    {
        facialAnimations ??= Array.Empty<BlendShapeWeightAnimation>();

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
}
