namespace Aoyon.FaceTune;

internal class FacialStyleContext
{
    public static bool TryGetFacialStyleAndObserve(GameObject target, [NotNullWhen(true)]out FacialStyleComponent? facialStyle, GameObject root, IObserveContext observeContext)
    {
        if (!observeContext.TryGetComponentInParent<FacialStyleComponent>(target, root, true, out facialStyle))
        {
            facialStyle = null;
            return false;
        }
        return true;
    }

    public static bool TryGetFacialStyle(GameObject target, [NotNullWhen(true)]out FacialStyleComponent? facialStyle)
    {
        facialStyle = target.GetComponentInParent<FacialStyleComponent>(true);
        if (facialStyle == null)
        {
            facialStyle = null;
            return false;
        }
        else
        {
            return true;
        }
    }

    public static bool TryGetFirstFacialStyleAndObserve(GameObject target, [NotNullWhen(true)]out FacialStyleComponent? facialStyle, IObserveContext observeContext)
    {
        using var _ = ListPool<FacialStyleComponent>.Get(out var facialStyles);
        observeContext.GetComponentsInChildren<FacialStyleComponent>(target, true, facialStyles);
        if (facialStyles.Count == 0)
        {
            facialStyle = null;
            return false;
        }
        else
        {
            facialStyle = facialStyles[0];
            return true;
        }
    }

    public static bool TryGetFacialStyleAnimations(GameObject target, ICollection<BlendShapeWeightAnimation> resultToAdd)
    {
        if (!TryGetFacialStyle(target, out var facialStyle))
        {
            return false;
        }
        facialStyle.GetBlendShapeAnimations(resultToAdd);
        return true;
    }

    public static bool TryGetFacialStyleAnimationsAndObserve(GameObject target, ICollection<BlendShapeWeightAnimation> resultToAdd, GameObject root, IObserveContext observeContext)
    {
        if (!TryGetFacialStyleAndObserve(target, out var facialStyle, root, observeContext))
        {
            return false;
        }
        facialStyle.GetBlendShapeAnimations(resultToAdd, observeContext);
        return true;
    }
}