namespace Aoyon.FaceTune;

internal static class ExpressionDataUtility
{
    public static void AddFirstFrameBlendShapes<T>(
        T component,
        ICollection<BlendShapeWeight> resultToAdd,
        string bodyPath,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null)
        where T : Component, IExpressionDataSource
    {
        foreach (var data in ResolveData(component))
        {
            AddFirstFrameBlendShapes(data, resultToAdd, bodyPath, facialAnimations);
        }
    }

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

    public static void AddAnimations<T>(
        T component,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        string bodyPath,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null)
        where T : Component, IExpressionDataSource
    {
        foreach (var data in ResolveData(component))
        {
            AddAnimations(data, resultToAdd, bodyPath, facialAnimations);
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

    public static IEnumerable<ExpressionData> ResolveData<T>(T source)
        where T : Component, IExpressionDataSource
    {
        if (source.DataReferenceMode != ComponentReferenceMode.Reference)
        {
            yield return source.Data;
            yield break;
        }

        var target = source.DataReference.Get(source);
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
