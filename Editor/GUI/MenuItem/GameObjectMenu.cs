using aoyon.facetune.importer;
using aoyon.facetune.build;
using nadena.dev.ndmf.runtime;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using M = UnityEditor.MenuItem;

namespace aoyon.facetune.gui;

internal static class GameObjectMenu
{
    [M(MenuItems.ImportFromFXLayerMenuPath, false, MenuItems.ImportFromFXLayerMenuPriority)] 
    static void ImportFromFXLayer()
    {
        var root = RuntimeUtil.FindAvatarInParents(Selection.activeGameObject?.transform);
        if (root == null)
        {
            throw new InvalidOperationException("failed to get avatar root");
        }
        var descriptor = root.GetComponent<VRCAvatarDescriptor>()!;
        foreach (var layer in descriptor.baseAnimationLayers)
        {
            if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX
                && layer.animatorController != null
                && layer.animatorController is AnimatorController ac)
            {
                var result = FXImporter.ImportFromVRChatFX(ac);
                if (result != null)
                {
                    result.transform.parent = root.transform;
                    Undo.RegisterCreatedObjectUndo(result, "Import FX Layer");
                    Selection.activeGameObject = result;
                }
                return;
            }
        }
        throw new InvalidOperationException("failed to find FX layer");
    }

    private static void IP(string guid, bool unpack = true)
    {
        var parent = Selection.activeGameObject;
        FTPrefabUtility.InstantiatePrefab(guid, unpack, parent);
    }
    
    [M(MenuItems.BaseTemplatePath, false, MenuItems.BaseTemplatePriority)] 
    static void TemplateBase() => IP("e643b160cc0f24a4fa8e33fb4df1fe7e");

    [M(MenuItems.ConditionPath, false, MenuItems.ConditionPriority)] 
    static void Condition() => IP("20aca02f84d174940bb4ca676555589a");
    
    [M(MenuItems.MenuSinglePath, false, MenuItems.MenuSinglePriority)] 
    static void MenuSingle() => IP("a045ae2cad411ae43b4c008ff814957e");

    [M(MenuItems.MenuExclusivePath, false, MenuItems.MenuExclusivePriority)] 
    static void MenuExclusive() => IP("9e1741e66ac069742976cf8c7e785a35");

    [M(MenuItems.MenuBlendingPath, false, MenuItems.MenuBlendingPriority)] 
    static void MenuBlending() => IP("557c13125870f764bb20173aa14b004f");

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
