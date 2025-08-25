using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal static class NDMFUtility
{
    public static bool TryGetComponent(this ComputeContext ctx, GameObject obj, Type type, [NotNullWhen(true)] out Component? component)
    {
        if (obj == null) { component = null; return false; }
        var c = ctx.GetComponent(obj, type);
        if (c == null) { component = null; return false; }
        component = c; return true;
    }
    public static bool TryGetComponent<T>(this ComputeContext ctx, GameObject obj, [NotNullWhen(true)] out T? component)
    where T : Component
    {
        if (obj == null) { component = null; return false; }
        var c = ctx.GetComponent<T>(obj);
        if (c == null) { component = null; return false; }
        component = c; return true;
    }
    public static bool TryGetComponentInParent<T>(this ComputeContext ctx, GameObject obj, GameObject root, bool includeInactive, [NotNullWhen(true)] out T? component)
    where T : Component
    {
        if (obj == null) { component = null; return false; }
        using var _ = ListPool<T>.Get(out var components);
        obj.GetComponentsInParent<T>(includeInactive, components);
        // GetComponentsInParentを監視できないのでその代わり
        using var _2 = ListPool<T>.Get(out var tmp);
        ctx.GetComponentsInChildren<T>(root, includeInactive, tmp);

        if (components.Count == 0)
        {
            component = null;
            return false;
        }
        else
        {
            component = components[0];
            return true;
        }
    }
    public static void GetComponentsInParent<T>(this ComputeContext ctx, GameObject obj, GameObject root, bool includeInactive, List<T> results)
    where T : Component
    {
        obj.GetComponentsInParent<T>(includeInactive, results);
        // GetComponentsInParentを監視できないのでその代わり
        ctx.GetComponentsInChildren<T>(root, includeInactive, results);
    }
    public static bool EditorOnlyInHierarchy(this ComputeContext ctx, GameObject obj)
    {
        foreach (var node in ctx.ObservePath(obj.transform))
        {
            var result = ctx.Observe(node.gameObject, go => go.CompareTag("EditorOnly"), (a, b) => a == b);
            if (result) return true;
        }
        return false;
    }
}