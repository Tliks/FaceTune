namespace Aoyon.FaceTune;

internal interface IObserveContext
{
    public GameObject? GetAvatarRoot(GameObject obj);
    public T Observe<T>(T obj) where T : Object;
    public R Observe<T, R>(T obj, Func<T, R> extract, Func<R, R, bool>? compare = null) where T : Object;
    public bool ActiveInHierarchy(GameObject obj);
    public bool EditorOnlyInHierarchy(GameObject obj);
    public void GetComponents<C>(GameObject obj, List<C> results) where C : Component;
    public void GetComponentsInChildren<C>(GameObject obj, bool includeInactive, List<C> results) where C : Component;
    public bool TryGetComponentInParent<C>(GameObject obj, GameObject root, bool includeInactive, [NotNullWhen(true)] out C? component) where C : Component;
    public void GetComponentsInParent<C>(GameObject obj, GameObject root, bool includeInactive, List<C> results) where C : Component;
}