using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune;

internal static partial class Utils
{
    public static string? GetRelativePath(GameObject root, GameObject child)
    {
        return RuntimeUtil.RelativePath(root, child);
    }

    public static List<GameObject> GetDirectChildren(this GameObject parent)
    {
        var result = new List<GameObject>();
        var transform = parent.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            result.Add(transform.GetChild(i).gameObject);
        }
        return result;
    }

    public static TComponent? FindDirectChildComponent<TComponent>(
        this Transform parent,
        string name,
        StringComparison comparison)
        where TComponent : Component
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (string.Equals(child.name, name, comparison)
                && child.TryGetComponent<TComponent>(out var component))
            {
                return component;
            }
        }

        return null;
    }

    /// <summary>
    /// rootからtargetまでのComponentをroot側から取得し、target自身は除外する。
    /// rootの子孫構成と取得順はComputeContext経由で監視する。
    /// </summary>
    public static T[] GetComponentsInParentExcludingSelf<T>(
        this ComputeContext context,
        GameObject root,
        Component target,
        bool includeInactive)
        where T : Component
        => FilterComponentsInParentExcludingSelf(
            context.GetComponentsInChildren<T>(root, includeInactive),
            target);

    /// <summary>
    /// rootからtargetまでのComponentをroot側から取得し、target自身は除外する。
    /// </summary>
    public static T[] GetComponentsInParentExcludingSelf<T>(
        this GameObject root,
        Component target,
        bool includeInactive)
        where T : Component
        => FilterComponentsInParentExcludingSelf(
            root.GetComponentsInChildren<T>(includeInactive),
            target);

    private static T[] FilterComponentsInParentExcludingSelf<T>(
        IEnumerable<T> components,
        Component target)
        where T : Component
    {
        var targetTransform = target.transform;
        return components
            .Where(component => component.transform != targetTransform
                                && targetTransform.IsChildOf(component.transform))
            .ToArray();
    }

    public static TComponent EnsureComponent<TComponent>(this GameObject gameObject) where TComponent : Component
    {
        var component = gameObject.GetComponent<TComponent>();
        if (component == null)
        {
            component = gameObject.AddComponent<TComponent>();
        }
        return component;
    }
}

