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
            data.Clip.GetFirstFrameBlendShapes(data.ClipOption, resultToAdd, bodyPath, facialAnimations);
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
            data.Clip.GetBlendShapeAnimations(data.ClipOption, resultToAdd, bodyPath, facialAnimations);
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
        => ResolveData(source, owner, new HashSet<IExpressionDataSource>());

    private static IEnumerable<ExpressionData> ResolveData(
        IExpressionDataSource source,
        Component owner,
        HashSet<IExpressionDataSource> resolving)
    {
        if (!resolving.Add(source)) yield break;

        var target = source.DataReference.Get(owner);
        if (target != null)
        {
            foreach (var referenced in target.GetComponents<FaceTuneTagComponent>().OfType<IExpressionDataSource>())
            {
                if (referenced is not Component referencedOwner) continue;
                foreach (var data in ResolveData(referenced, referencedOwner, resolving))
                    yield return data;
            }
        }

        yield return source.Data;
        resolving.Remove(source);
    }
}
