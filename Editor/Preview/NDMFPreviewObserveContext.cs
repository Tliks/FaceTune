using nadena.dev.ndmf.preview;

namespace com.aoyon.facetune;

internal class NDMFPreviewObserveContext : IOberveContext
{
    public ComputeContext Context { get; }

    public NDMFPreviewObserveContext(ComputeContext context)
    {
        Context = context;
    }

    public GameObject? GetAvatarRoot(GameObject obj)
    {
        return Context.GetAvatarRoot(obj);
    }

    public R? Observe<T, R>(T obj, Func<T, R?> extract, Func<R, R, bool>? compare = null) where T : Object
    {
        return Context.Observe(obj, extract!, compare);
    }

    public bool ActiveInHierarchy(GameObject obj)
    {
        return Context.ActiveInHierarchy(obj);
    }

    public C? GetComponentNullable<C>(GameObject obj) where C : Component
    {
        return Context.GetComponent<C>(obj).NullCast();
    }

    public C[] GetComponents<C>(GameObject obj) where C : Component
    {
        return Context.GetComponents<C>(obj);
    }

    public C[] GetComponentsInChildren<C>(GameObject obj, bool includeInactive) where C : Component
    {
        return Context.GetComponentsInChildren<C>(obj, includeInactive);
    }
}