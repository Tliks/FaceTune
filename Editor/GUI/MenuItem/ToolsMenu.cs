using UnityEditorInternal;
using aoyon.facetune.Settings;
using aoyon.facetune.gui.shapes_editor;

namespace aoyon.facetune.gui;

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
        Menu.SetChecked(MenuItems.SelectedExpressionPreviewPath, ProjectSettings.EnableSelectedExpressionPreview);
        return true;
    }

    [MenuItem(MenuItems.SelectedExpressionPreviewPath, false, MenuItems.SelectedExpressionPreviewPriority)]
    private static void ToggleSelectedExpressionPreview()
    {
        ProjectSettings.EnableSelectedExpressionPreview = !ProjectSettings.EnableSelectedExpressionPreview;
        InternalEditorUtility.RepaintAllViews();
    }
}
