namespace com.aoyon.facetune;

internal interface IOberveContext
{
    public GameObject? GetAvatarRoot(GameObject obj);
    public R? Observe<T, R>(T obj, Func<T, R?> extract, Func<R, R, bool>? compare = null) where T : Object;
    public bool ActiveInHierarchy(GameObject obj);
    public C? GetComponentNullable<C>(GameObject obj) where C : Component;
    public C[] GetComponents<C>(GameObject obj) where C : Component;
    public C[] GetComponentsInChildren<C>(GameObject obj, bool includeInactive) where C : Component;
}
