namespace aoyon.facetune;

internal interface IObserveContext
{
    public GameObject? GetAvatarRoot(GameObject obj);
    public T Observe<T>(T obj) where T : Object;
    public R Observe<T, R>(T obj, Func<T, R> extract, Func<R, R, bool>? compare = null) where T : Object;
    public bool ActiveInHierarchy(GameObject obj);
    public C? GetComponentNullable<C>(GameObject obj) where C : Component;
    public void GetComponents<C>(GameObject obj, List<C> results) where C : Component;
    public void GetComponentsInChildren<C>(GameObject obj, bool includeInactive, List<C> results) where C : Component;
}
