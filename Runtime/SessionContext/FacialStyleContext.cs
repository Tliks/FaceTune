namespace Aoyon.FaceTune;

internal class FacialStyleContext
{
    public static bool TryGetFacialStyleAndObserve(GameObject target, [NotNullWhen(true)]out FacialStyleComponent? facialStyle, GameObject root, IObserveContext observeContext)
    {
        using var _ = ListPool<FacialStyleComponent>.Get(out var facialStyleComponents);
        target.GetComponentsInParent<FacialStyleComponent>(true, facialStyleComponents);
        // GetComponentsInParentを監視できないのでその代わり
        using var _2 = ListPool<FacialStyleComponent>.Get(out var tmp);
        observeContext.GetComponentsInChildren<FacialStyleComponent>(root, true, tmp);

        if (facialStyleComponents.Count == 0)
        {
            facialStyle = null;
            return false;
        }
        else
        {
            var nearset = facialStyleComponents[0];
            facialStyle = nearset;
            return true;
        }
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

    public static bool TryGetFacialStyleShapesAndObserve(GameObject target, ICollection<BlendShape> resultToAdd, GameObject root, IObserveContext observeContext)
    {
        if (!TryGetFacialStyleAndObserve(target, out var facialStyle, root, observeContext))
        {
            return false;
        }
        facialStyle.GetBlendShapes(resultToAdd, observeContext);
        return true;
    }
    
    public static bool TryGetFacialStyleShapes(GameObject target, ICollection<BlendShape> resultToAdd)
    {
        if (!TryGetFacialStyle(target, out var facialStyle))
        {
            return false;
        }
        facialStyle.GetBlendShapes(resultToAdd);
        return true;
    }

    public static bool TryGetFacialStyleAnimations(GameObject target, ICollection<BlendShapeAnimation> resultToAdd)
    {
        if (!TryGetFacialStyle(target, out var facialStyle))
        {
            return false;
        }
        facialStyle.GetBlendShapeAnimations(resultToAdd);
        return true;
    }

    public static bool TryGetFacialStyleAnimationsAndObserve(GameObject target, ICollection<BlendShapeAnimation> resultToAdd, GameObject root, IObserveContext observeContext)
    {
        if (!TryGetFacialStyleAndObserve(target, out var facialStyle, root, observeContext))
        {
            return false;
        }
        facialStyle.GetBlendShapeAnimations(resultToAdd, observeContext);
        return true;
    }
}