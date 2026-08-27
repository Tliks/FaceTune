using M = UnityEditor.MenuItem;
using UnityEditorInternal;
using Aoyon.FaceTune.Settings;
using Aoyon.FaceTune.Gui.ShapesEditor;
using nadena.dev.ndmf.runtime;

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

    public const string WindowPath = GameObjectPath + "Window";
    public const int WindowPriority = 100;

}


internal static class GameObjectMenu
{
    [M(MenuItems.WindowPath, false, MenuItems.WindowPriority)]
    private static void OpenWindow()
    {
        var avatarRoot = GetSelectedAvatarRoot();
        if (avatarRoot != null) FaceTuneWindow.Open(avatarRoot);
    }

    [M(MenuItems.WindowPath, true)]
    private static bool ValidateOpenWindow()
    {
        var avatarRoot = GetSelectedAvatarRoot();
        return avatarRoot != null && Selection.activeGameObject == avatarRoot;
    }

    private static GameObject? GetSelectedAvatarRoot()
    {
        var selected = Selection.activeGameObject;
        if (selected == null) return null;
        var root = RuntimeUtil.FindAvatarInParents(selected.transform);
        return root == null ? null : root.gameObject;
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
