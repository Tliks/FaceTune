using nadena.dev.ndmf.preview;

namespace com.aoyon.facetune;

internal class NDMFPreviewObserveContext : IObserveContext
{
    private readonly ComputeContext _context;

    public NDMFPreviewObserveContext(ComputeContext context)
    {
        _context = context;
    }

    public GameObject? GetAvatarRoot(GameObject obj)
    {
        return _context.GetAvatarRoot(obj);
    }

    public R Observe<T, R>(T obj, Func<T, R> extract, Func<R, R, bool>? compare = null) where T : Object
    {
        return _context.Observe(obj, extract, compare);
    }

    public bool ActiveInHierarchy(GameObject obj)
    {
        return _context.ActiveInHierarchy(obj);
    }

    public C? GetComponentNullable<C>(GameObject obj) where C : Component
    {
        return _context.GetComponent<C>(obj).NullCast();
    }

    public C[] GetComponents<C>(GameObject obj) where C : Component
    {
        return _context.GetComponents<C>(obj);
    }

    public C[] GetComponentsInChildren<C>(GameObject obj, bool includeInactive) where C : Component
    {
        return _context.GetComponentsInChildren<C>(obj, includeInactive);
    }
}