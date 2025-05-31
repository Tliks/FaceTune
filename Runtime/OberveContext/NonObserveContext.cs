using nadena.dev.ndmf.runtime;

namespace com.aoyon.facetune;

internal class NonObserveContext : IOberveContext
{
    public GameObject? GetAvatarRoot(GameObject obj)
    {
        return RuntimeUtil.FindAvatarInParents(obj.transform).NullCast()?.gameObject;
    }

    public R Observe<T, R>(T obj, Func<T, R> extract, Func<R, R, bool>? compare = null) where T : Object
    {
        return extract(obj);
    }

    public bool ActiveInHierarchy(GameObject obj)
    {
        return obj.activeInHierarchy;
    }

    public C? GetComponentNullable<C>(GameObject obj) where C : Component
    {
        return obj.GetComponentNullable<C>();
    }

    public C[] GetComponents<C>(GameObject obj) where C : Component
    {
        return obj.GetComponents<C>();
    }

    public C[] GetComponentsInChildren<C>(GameObject obj, bool includeInactive) where C : Component
    {
        return obj.GetComponentsInChildren<C>(includeInactive);
    }
}