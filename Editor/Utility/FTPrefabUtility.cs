namespace aoyon.facetune;

internal static class FTPrefabUtility
{
    public static void InstantiatePrefab(string guid, 
        bool unpackRoot = false,
        GameObject? parent = null, 
        bool isFirstSibling = false
    )
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        if (prefab == null)
        {
            Debug.LogError("Prefab not found");
            return;
        }

        InstantiatePrefab(prefab, unpackRoot, parent, isFirstSibling);
    }

    public static void InstantiatePrefab(GameObject prefab, 
        bool unpackRoot = false,
        GameObject? parent = null, 
        bool isFirstSibling = false
    )
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Create " + prefab.name);
        var groupIndex = Undo.GetCurrentGroup();
        
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Create " + instance.name);
        
        if (parent != null)
        {
            Undo.SetTransformParent(instance.transform, parent.transform, "Set Parent");
        }
        
        if (isFirstSibling)
        {
            Undo.SetSiblingIndex(instance.transform, 0, "Set First Sibling");
        }
        
        if (unpackRoot)
        {
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
        }

        Selection.activeObject = instance;
        
        Undo.CollapseUndoOperations(groupIndex);
    }
}