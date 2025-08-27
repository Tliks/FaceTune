namespace Aoyon.FaceTune;

internal static class PrefabUtility
{
    public static GameObject InstantiatePrefab(string guid, 
        bool unpack,
        GameObject? parent = null, 
        bool isFirstSibling = false
    )
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        if (prefab == null)
        {
            throw new Exception("Prefab not found");
        }

        return InstantiatePrefab(prefab, unpack, parent, isFirstSibling);
    }

    public static GameObject InstantiatePrefab(GameObject prefab, 
        bool unpack,
        GameObject? parent = null, 
        bool isFirstSibling = false
    )
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Create " + prefab.name);
        var groupIndex = Undo.GetCurrentGroup();
        
        var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Create " + instance.name);
        
        if (parent != null)
        {
            Undo.SetTransformParent(instance.transform, parent.transform, "Set Parent");
        }
        
        if (isFirstSibling)
        {
            Undo.SetSiblingIndex(instance.transform, 0, "Set First Sibling");
        }
        
        if (unpack)
        {
            UnityEditor.PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        }

        Selection.activeObject = instance;
        
        Undo.CollapseUndoOperations(groupIndex);

        return instance;
    }
}