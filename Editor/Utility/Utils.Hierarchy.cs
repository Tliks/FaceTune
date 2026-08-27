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

    /// <summary>自身のGameObjectを除く親からComponentを取得する。</summary>
    public static T[] GetComponentsInParentExcludingSelf<T>(this Component component, bool includeInactive)
        where T : Component
    {
        var parent = component.transform.parent;
        return parent != null
            ? parent.GetComponentsInParent<T>(includeInactive)
            : Array.Empty<T>();
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

    public static bool IsEditorOnlyInHierarchy(this GameObject gameObject)
    {
        var current = gameObject;
        while (current != null)
        {
            if (current.CompareTag("EditorOnly"))
            {
                return true;
            }
            var parent = current.transform.parent;
            current = parent != null ? parent.gameObject : null;
        }
        return false;
    }
}

