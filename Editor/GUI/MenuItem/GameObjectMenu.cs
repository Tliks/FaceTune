using Aoyon.FaceTune.Importer;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.runtime;
using M = UnityEditor.MenuItem;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Gui;

internal static class GameObjectMenu
{
    private static GameObject IP(string guid, bool unpack = true, bool isFirstSibling = false, bool addInstaller = false)
    {
        var parent = Selection.activeGameObject;
        return PrefabAssets.InstantiatePrefab(guid, unpack: unpack, parent: parent, isFirstSibling: isFirstSibling, addInstaller: addInstaller);
    }
    
    [M(MenuItems.TemplatePath, false, MenuItems.TemplatePriority)] 
    static void Template() => IP("e643b160cc0f24a4fa8e33fb4df1fe7e");

    [M(MenuItems.ImportFxPath, false, MenuItems.ImportFxPriority)] 
    static void ImportFx() {
        var root = IP("e643b160cc0f24a4fa8e33fb4df1fe7e");
        if (!CustomEditorUtility.TryGetContext(root, out var context)) throw new Exception("Failed to get context");
        var support = MetabasePlatformSupport.GetSupportInParents(context.Root.transform);
        var animatorController = support?.GetAnimatorController();
        if (animatorController == null) throw new Exception("Failed to get animator controller");
        var importer = new AnimatorControllerImporter(context, animatorController);
        importer.Import(root);
    }

    [M(MenuItems.ConditionPath, false, MenuItems.ConditionPriority)] 
    static void Condition() => IP("20aca02f84d174940bb4ca676555589a");
    
    [M(MenuItems.MenuSinglePath, false, MenuItems.MenuSinglePriority)] 
    static void MenuSingle() => IP("a045ae2cad411ae43b4c008ff814957e", addInstaller: true); // Installerが必要

    [M(MenuItems.MenuExclusivePath, false, MenuItems.MenuExclusivePriority)] 
    static void MenuExclusive() => IP("9e1741e66ac069742976cf8c7e785a35", addInstaller: true); // Installerが必要

    [M(MenuItems.MenuBlendingPath, false, MenuItems.MenuBlendingPriority)] 
    static void MenuBlending() => IP("557c13125870f764bb20173aa14b004f", addInstaller: true); // Installerが必要

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
