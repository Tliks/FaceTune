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

    public static bool TryGetComponentInParent<T>(this GameObject gameObject, bool includeInactive, [NotNullWhen(true)] out T? result) where T : Component
    {
        result = gameObject.GetComponentInParent<T>(includeInactive);
        return result != null;
    }

    public static bool TryGetComponentInParent<T>(this Component component, bool includeInactive, [NotNullWhen(true)] out T? result) where T : Component
    {
        return TryGetComponentInParent<T>(component.gameObject, includeInactive, out result);
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

