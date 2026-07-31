using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal static class ExpressionDataExtensions
{
    public static void GetAnimations<T>(
        this T component,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        string bodyPath,
        ComputeContext? context = null)
        where T : Component, IHasExpressionData
    {
        GetAnimations(
            component,
            component,
            resultToAdd,
            bodyPath,
            new HashSet<IHasExpressionData>(),
            context ?? ComputeContext.NullContext);
    }

    private static void GetAnimations(
        IHasExpressionData source,
        Component owner,
        ICollection<BlendShapeWeightAnimation> result,
        string bodyPath,
        HashSet<IHasExpressionData> resolving,
        ComputeContext context)
    {
        if (!resolving.Add(source)) return;

        context.Observe(owner);

        var data = source.Data;
        if (data.Clip != null)
        {
            data.Clip.GetBlendShapeAnimations(data.ClipOption, result, bodyPath);
        }

        foreach (var (referenced, referencedOwner) in GetReferencedSources(source, owner, context))
        {
            GetAnimations(
                referenced,
                referencedOwner,
                result,
                bodyPath,
                resolving,
                context);
        }

        foreach (var animation in data.BlendShapeAnimations)
        {
            result.Add(animation);
        }

        resolving.Remove(source);
    }

    private static IEnumerable<(IHasExpressionData Source, Component Owner)> GetReferencedSources(
        IHasExpressionData source,
        Component owner,
        ComputeContext context)
    {
        var target = source.Data.DataReference.Get(owner);
        if (target == null) yield break;

        var components = context.GetComponents<FaceTuneTagComponent>(target);
        foreach (var component in components)
        {
            if (component is IHasExpressionData referenced)
            {
                yield return (referenced, component);
            }
        }
    }

    public static IEnumerable<ExpressionData> EnumerateDataGraph(this Component owner, ComputeContext? context = null)
    {
        if (owner is not IHasExpressionData source) yield break;

        foreach (var data in EnumerateDataGraph(
                     source,
                     owner,
                     new HashSet<IHasExpressionData>(),
                     context ?? ComputeContext.NullContext))
            yield return data;
    }

    private static IEnumerable<ExpressionData> EnumerateDataGraph(
        IHasExpressionData source,
        Component owner,
        HashSet<IHasExpressionData> resolving,
        ComputeContext context)
    {
        if (!resolving.Add(source)) yield break;

        context.Observe(owner);
        yield return source.Data;

        var target = source.Data.DataReference.Get(owner);
        if (target != null)
        {
            foreach (var referenced in context.GetComponents<FaceTuneTagComponent>(target).OfType<IHasExpressionData>())
            {
                if (referenced is not Component referencedOwner) continue;

                foreach (var data in EnumerateDataGraph(referenced, referencedOwner, resolving, context))
                {
                    yield return data;
                }
            }
        }

        resolving.Remove(source);
    }
}
