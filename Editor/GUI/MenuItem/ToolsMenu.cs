using UnityEditorInternal;
using Aoyon.FaceTune.Settings;
using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

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
