using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal class FacialStyleContext
{
    public static bool TryGetFacialStyle(GameObject target, [NotNullWhen(true)] out StyleComponent? facialStyle)
    {
        facialStyle = target.GetComponentInParent<StyleComponent>(true);
        return facialStyle != null;
    }

    public static bool TryGetFacialStyle(
        GameObject target,
        [NotNullWhen(true)] out StyleComponent? facialStyle,
        GameObject root,
        ComputeContext? context = null)
    {
        context ??= ComputeContext.NullContext;
        return context.TryGetComponentInParent(target, root, true, out facialStyle);
    }

    public static bool TryGetFirstFacialStyle(
        GameObject target,
        [NotNullWhen(true)] out StyleComponent? facialStyle,
        ComputeContext? context = null)
    {
        context ??= ComputeContext.NullContext;
        using var _ = ListPool<StyleComponent>.Get(out var facialStyles);
        context.GetComponentsInChildren(target, true, facialStyles);
        if (facialStyles.Count == 0)
        {
            facialStyle = null;
            return false;
        }

        facialStyle = facialStyles[0];
        return true;
    }

    public static bool TryGetFacialStyleAnimations(GameObject target, ICollection<BlendShapeWeightAnimation> resultToAdd)
    {
        if (!TryGetFacialStyle(target, out var facialStyle)) return false;
        ExpressionDataUtility.ResolveStyleAnimations(facialStyle, resultToAdd);
        return true;
    }

    public static bool TryGetFacialStyleAnimations(
        GameObject target,
        ICollection<BlendShapeWeightAnimation> resultToAdd,
        GameObject root,
        ComputeContext? context = null)
    {
        context ??= ComputeContext.NullContext;
        if (!TryGetFacialStyle(target, out var facialStyle, root, context)) return false;
        context.Observe(facialStyle);
        ExpressionDataUtility.ResolveStyleAnimations(facialStyle, resultToAdd);
        return true;
    }
}
