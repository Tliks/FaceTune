using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal static class NDMFExtensions
{
    public static bool TryGetComponentInParent<T>(this ComputeContext ctx, GameObject obj, GameObject root, bool includeInactive, [NotNullWhen(true)] out T? component)
    where T : Component
    {
        if (obj == null) { component = null; return false; }
        using var _ = ListPool<T>.Get(out var components);
        ctx.GetComponentsInParent(obj, root, includeInactive, components);

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
        if (obj == null
            || obj.transform != root.transform && !obj.transform.IsChildOf(root.transform))
            return;

        // 親子関係の変更も再計算対象にする。
        foreach (var _ in ctx.ObservePath(obj.transform))
        {
        }

        for (var current = obj.transform; current != null; current = current.parent)
        {
            if (includeInactive || current.gameObject.activeInHierarchy)
                results.AddRange(ctx.GetComponents<T>(current.gameObject));

            if (current.gameObject == root)
                break;
        }
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