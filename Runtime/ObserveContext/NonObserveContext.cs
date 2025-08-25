using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune;

internal class NonObserveContext : IObserveContext
{
    public GameObject? GetAvatarRoot(GameObject obj)
    {
        return RuntimeUtil.FindAvatarInParents(obj.transform).DestroyedAsNull()?.gameObject;
    }

    public T Observe<T>(T obj) where T : Object
    {
        return obj;
    }

    public R Observe<T, R>(T obj, Func<T, R> extract, Func<R, R, bool>? compare = null) where T : Object
    {
        return extract(obj);
    }

    public bool ActiveInHierarchy(GameObject obj)
    {
        return obj.activeInHierarchy;
    }

    public bool EditorOnlyInHierarchy(GameObject obj)
    {
        return obj.IsEditorOnlyInHierarchy();
    }

    public C? GetComponentNullable<C>(GameObject obj) where C : Component
    {
        return obj.GetComponentNullable<C>();
    }

    public void GetComponents<C>(GameObject obj, List<C> results) where C : Component
    {
        obj.GetComponents<C>(results);
    }

    public void GetComponentsInChildren<C>(GameObject obj, bool includeInactive, List<C> results) where C : Component
    {
        obj.GetComponentsInChildren<C>(includeInactive, results);
    }

    public bool TryGetComponentInParent<C>(GameObject obj, GameObject root, bool includeInactive, [NotNullWhen(true)] out C? component) where C : Component
    {
        component = obj.GetComponentInParent<C>(includeInactive);
        return component != null;
    }

    public void GetComponentsInParent<C>(GameObject obj, GameObject root, bool includeInactive, List<C> results) where C : Component
    {
        obj.GetComponentsInParent<C>(includeInactive, results);
    }
}