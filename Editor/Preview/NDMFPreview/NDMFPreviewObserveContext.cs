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

    public T Observe<T>(T obj) where T : Object
    {
        return _context.Observe(obj);
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
        return _context.GetComponent<C>(obj).DestroyedAsNull();
    }

    public void GetComponents<C>(GameObject obj, List<C> results) where C : Component
    {
        _context.GetComponents<C>(obj, results);
    }

    public void GetComponentsInChildren<C>(GameObject obj, bool includeInactive, List<C> results) where C : Component
    {
        _context.GetComponentsInChildren<C>(obj, includeInactive, results);
    }
}