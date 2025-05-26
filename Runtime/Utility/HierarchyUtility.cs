using nadena.dev.ndmf.runtime;

namespace com.aoyon.facetune;

internal static class HierarchyUtility
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

    public static T? GetComponentNullable<T>(this GameObject gameObject) where T : Component
    {
        //return gameObject.GetComponent<T>().NullCast();

        if (gameObject.TryGetComponent<T>(out var component))
        {
            return component;
        }

        return null;
    }

    public static T? GetComponentNullable<T>(this Component component) where T : Component
    {
        return GetComponentNullable<T>(component.gameObject);
    }

    public static T? GetComponentInParentNullable<T>(this GameObject gameObject, bool includeInactive = false) where T : Component
    {
        return gameObject.GetComponentInParent<T>(includeInactive).NullCast();
    }

    public static T? GetComponentInParentNullable<T>(this Component component, bool includeInactive = false) where T : Component
    {
        return GetComponentInParentNullable<T>(component.gameObject, includeInactive);
    }

    public static T? GetComponentInChildrenNullable<T>(this GameObject gameObject, bool includeInactive = false) where T : Component
    {
        return gameObject.GetComponentInChildren<T>(includeInactive).NullCast();
    }

    public static T? GetComponentInChildrenNullable<T>(this Component component, bool includeInactive = false) where T : Component
    {
        return GetComponentInChildrenNullable<T>(component.gameObject, includeInactive);
    }

    public static List<TInterface> GetInterfacesInChildComponents<TComponent, TInterface>(this GameObject gameObject, bool includeInactive = false)
    where TComponent : Component where TInterface : class
    {
        return gameObject
            .GetComponentsInChildren<TComponent>(includeInactive)
            .UnityOfType<TInterface>()
            .ToList();
    }

    public static List<TInterface> GetInterfacesInChildComponents<TComponent, TInterface>(this Component component, bool includeInactive = false)
    where TComponent : Component where TInterface : class
    {
        return GetInterfacesInChildComponents<TComponent, TInterface>(component.gameObject, includeInactive);
    }

    public static List<TInterface> GetInterfacesInChildFTComponents<TInterface>(this GameObject gameObject, bool includeInactive = false)
    where TInterface : class
    {
        return GetInterfacesInChildComponents<FaceTuneTagComponent, TInterface>(gameObject, includeInactive);
    }

    public static List<TInterface> GetInterfacesInChildFTComponents<TInterface>(this Component component, bool includeInactive = false)
    where TInterface : class
    {
        return GetInterfacesInChildFTComponents<TInterface>(component.gameObject, includeInactive);
    }

    public static TComponent[] GetDirectChildComponents<TComponent>(this GameObject gameObject) where TComponent : Component
    {
        return gameObject.GetDirectChildren()
            .Select(c => c.GetComponent<TComponent>())
            .Where(c => c != null)
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

