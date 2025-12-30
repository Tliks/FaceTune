using Aoyon.FaceTune.Importer;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.runtime;
using M = UnityEditor.MenuItem;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Gui;

internal static class GameObjectMenu
{
    private static void IP(string guid, bool addInstaller = false)
    {
        var parent = Selection.activeGameObject;
        PrefabUtility.InstantiatePrefab(guid, unpack: true, parent: parent, isFirstSibling: false, addInstaller: addInstaller);
    }
    
    [M(MenuItems.TemplatePath, false, MenuItems.TemplatePriority)] 
    static void Template() => IP("e643b160cc0f24a4fa8e33fb4df1fe7e");


    [M(MenuItems.ConditionPath, false, MenuItems.ConditionPriority)] 
    static void Condition() => IP("20aca02f84d174940bb4ca676555589a");
    
    [M(MenuItems.MenuSinglePath, false, MenuItems.MenuSinglePriority)] 
    static void MenuSingle() => IP("a045ae2cad411ae43b4c008ff814957e", true);

    [M(MenuItems.MenuExclusivePath, false, MenuItems.MenuExclusivePriority)] 
    static void MenuExclusive() => IP("9e1741e66ac069742976cf8c7e785a35", true);

    [M(MenuItems.MenuBlendingPath, false, MenuItems.MenuBlendingPriority)] 
    static void MenuBlending() => IP("557c13125870f764bb20173aa14b004f", true);

    [M(MenuItems.DebugModifyHierarchyPassPath, false, MenuItems.DebugModifyHierarchyPassPriority)]
    static void DebugModifyHierarchyPass()
    {
        var root = RuntimeUtil.FindAvatarInParents(Selection.activeGameObject?.transform);
        if (root == null) return;

        root = Object.Instantiate(root);
        var buildPassState = new BuildPassState(root.gameObject);
        if (buildPassState.TryGetBuildPassContext(out var buildPassContext) is false) return;

        ModifyHierarchyPass.Excute(buildPassContext);
    }
}
