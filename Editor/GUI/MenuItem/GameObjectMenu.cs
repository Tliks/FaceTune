using com.aoyon.facetune.ndmf;
using nadena.dev.ndmf.runtime;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using M = UnityEditor.MenuItem;

namespace com.aoyon.facetune.ui;

internal static class GameObjectMenu
{
    private const string BasePath = $"GameObject/{FaceTuneConsts.Name}/";
    private const int PRIORITY = 21;

    private const string FXImporterPath = $"{BasePath}/Import from FX Layer";
    [M(FXImporterPath, false, PRIORITY)] 
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

    private static void IP(string guid, 
        bool unpackRoot = false
    )
    {
        var parent = Selection.activeGameObject;
        FTPrefabUtility.InstantiatePrefab(guid, unpackRoot, parent);
    }
    
    private const int PrefabPriority = PRIORITY + 10;
    
    [M(BasePath + "Template Base", false, PrefabPriority)] 
    static void TemplateBase() => IP("e643b160cc0f24a4fa8e33fb4df1fe7e", true);

    [M(BasePath + "Condition", false, PrefabPriority + 1)] 
    static void Condition() => IP("20aca02f84d174940bb4ca676555589a", true);
    
    private const string MenuPath = BasePath + "Menu/";
    [M(MenuPath + "single", false, PrefabPriority + 2)] 
    static void MenuSingle() => IP("a045ae2cad411ae43b4c008ff814957e", true);

    [M(MenuPath + "exclusive", false, PrefabPriority + 3)] 
    static void MenuExclusive() => IP("9e1741e66ac069742976cf8c7e785a35", true);

    [M(MenuPath + "blending", false, PrefabPriority + 4)] 
    static void MenuBlending() => IP("557c13125870f764bb20173aa14b004f", true);

    private const string DebugPath = BasePath + "Debug/";

    [M(DebugPath + "Excute ModifyHierarchyPass", false, PrefabPriority + 5)]
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
