namespace Aoyon.FaceTune;

internal static class ExpressionDataExtensions
{
    public static void GetFirstFrameBlendShapes<T>(
        this T component,
        ICollection<BlendShapeWeight> resultToAdd,
        string? bodyPath,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null)
        where T : Component, IExpressionDataSource
    {
        foreach (var data in ResolveData(component))
        {
            GetFirstFrameBlendShapes(data, resultToAdd, bodyPath, facialAnimations);
        }
    }

    public static void GetFirstFrameBlendShapes(
        ExpressionData data,
        ICollection<BlendShapeWeight> resultToAdd,
        string? bodyPath,
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

    public static void GetAnimations<T>(
        this T component,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        string? bodyPath,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null)
        where T : Component, IExpressionDataSource
    {
        foreach (var data in ResolveData(component))
        {
            GetAnimations(data, resultToAdd, bodyPath, facialAnimations);
        }
    }

    public static void GetAnimations(
        this ExpressionData data,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        string? bodyPath,
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

    public static IEnumerable<ExpressionData> ResolveData<T>(this T source)
        where T : Component, IExpressionDataSource
    {
        return ResolveData(source, source);
    }

    public static IEnumerable<ExpressionData> ResolveData(this IExpressionDataSource source, Component owner)
    {
        if (source.DataReferenceMode != ComponentReferenceMode.Reference)
        {
            yield return source.Data;
            yield break;
        }

        var target = source.DataReference.Get(owner);
        if (target == null) yield break;

        foreach (var component in target.GetComponents<FaceTuneTagComponent>().OfType<IExpressionDataSource>())
        {
            // 1段階までの参照解決
            if (component.DataReferenceMode == ComponentReferenceMode.Direct)
            {
                yield return component.Data;
            }
        }
    }
}
