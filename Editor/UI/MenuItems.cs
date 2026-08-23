using M = UnityEditor.MenuItem;
using Aoyon.FaceTune.Platforms;
using UnityEditorInternal;
using Aoyon.FaceTune.Settings;
using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

internal static class MenuItems
{
    // Tools
    private const string ToolsPath = "Tools/" + FaceTuneConstants.Name + "/";
    
    public const string FacialShapesEditorPath = ToolsPath + "Facial Shapes Editor";
    public const int FacialShapesEditorPriority = 1000;

    private const string ToolsSettingsPath = ToolsPath + "Settings/";
    public const string SelectedExpressionPreviewPath = ToolsSettingsPath + "Selected Expression Preview";
    public const int SelectedExpressionPreviewPriority = 1100;

    public const string ProjectSelectedExpressionPreviewPath = ToolsSettingsPath + "Project Selected Expression Preview";
    public const int ProjectSelectedExpressionPreviewPriority = 1101;

    private const string ToolsDebugPath = ToolsPath + "Debug/";
    public const string ReloadLocalizationPath = ToolsDebugPath + "Reload Localization";
    public const int ReloadLocalizationPriority = 1200;

    // Assets
    private const string AssetsPath = "Assets/" + FaceTuneConstants.Name + "/";

    public const string EditAnimationClipMenuPath = AssetsPath + "Edit Animation Clip";
    public const int EditAnimationClipMenuPriority = 1000;


    // GameObject
    private const string GameObjectPath = "GameObject/" + FaceTuneConstants.Name + "/";

    public const string TemplatePath = GameObjectPath + "Template";
    public const int TemplatePriority = 100;

    public const string ImportFxPath = GameObjectPath + "Import FX";
    public const int ImportFxPriority = 101;

}


internal static class GameObjectMenu
{
    private static GameObject IP(string guid, bool unpack = true)
    {
        var parent = Selection.activeGameObject;
        return Utils.InstantiatePrefab(guid, unpack: unpack, parent: parent);
    }
    
    [M(MenuItems.TemplatePath, false, MenuItems.TemplatePriority)] 
    static GameObject Template() {
        var root = IP("e643b160cc0f24a4fa8e33fb4df1fe7e", unpack: false);

        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
        foreach (Transform child in root.transform)
        {
            if (child.name == "Option") continue;
            PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        }

        return root;
    }

    [M(MenuItems.ImportFxPath, false, MenuItems.ImportFxPriority)] 
    static void ImportFx() {
        var selected = Selection.activeGameObject;
        if (selected == null) throw new InvalidOperationException("No GameObject selected");
        if (!AvatarContext.TryGet(selected, out var context, out _)) throw new Exception("Failed to get context");
        var candidates = MetabasePlatformSupport.GetForAvatar(context.Root.transform)
            .Select(support => (
                Support: support,
                Controller: support.GetAnimatorController().DestroyedAsNull()))
            .Where(candidate => candidate.Controller != null)
            .Select(candidate => (candidate.Support, Controller: candidate.Controller!))
            .ToArray();
        if (candidates.Length != 1) throw new Exception("Failed to uniquely identify an animator controller");

        var (support, animatorController) = candidates[0];
        support.ImportAnimatorController(context, animatorController, selected);
    }

}

internal static class ToolsMenu
{
    [MenuItem(MenuItems.FacialShapesEditorPath, false, MenuItems.FacialShapesEditorPriority)]
    private static void OpenFacialShapesEditor()
    {
        FacialShapesEditor.TryOpenEditor(targeting: new AnimationClipTargeting());
    }

    [MenuItem(MenuItems.SelectedExpressionPreviewPath, true)]
    private static bool ValidateSelectedExpressionPreview()
    {
        Menu.SetChecked(MenuItems.SelectedExpressionPreviewPath, ProjectSettings.EnableHierarchySelectedExpressionPreview);
        return true;
    }

    [MenuItem(MenuItems.SelectedExpressionPreviewPath, false, MenuItems.SelectedExpressionPreviewPriority)]
    private static void ToggleSelectedExpressionPreview()
    {
        ProjectSettings.EnableHierarchySelectedExpressionPreview = !ProjectSettings.EnableHierarchySelectedExpressionPreview;
        InternalEditorUtility.RepaintAllViews();
    }

    [MenuItem(MenuItems.ProjectSelectedExpressionPreviewPath, true)]
    private static bool ValidateProjectSelectedExpressionPreview()
    {
        Menu.SetChecked(
            MenuItems.ProjectSelectedExpressionPreviewPath,
            ProjectSettings.EnableProjectSelectedExpressionPreview);
        return true;
    }

    [MenuItem(MenuItems.ProjectSelectedExpressionPreviewPath, false, MenuItems.ProjectSelectedExpressionPreviewPriority)]
    private static void ToggleProjectSelectedExpressionPreview()
    {
        ProjectSettings.EnableProjectSelectedExpressionPreview = !ProjectSettings.EnableProjectSelectedExpressionPreview;
        InternalEditorUtility.RepaintAllViews();
    }
}
