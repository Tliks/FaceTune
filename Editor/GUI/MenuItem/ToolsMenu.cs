using UnityEditorInternal;
using aoyon.facetune.Settings;

namespace aoyon.facetune.gui;

internal static class ToolsMenu
{
    private const string BasePath = $"Tools/{FaceTuneConsts.Name}/";

    private const string Tools_SelectedExpressionPreviewPath = BasePath + "SelectedExpressionPreview";

    [MenuItem(Tools_SelectedExpressionPreviewPath, true)]
    private static bool ValidateSelectedExpressionPreview()
    {
        Menu.SetChecked(Tools_SelectedExpressionPreviewPath, ProjectSettings.EnableSelectedExpressionPreview);
        return true;
    }

    [MenuItem(Tools_SelectedExpressionPreviewPath, false)]
    private static void ToggleSelectedExpressionPreview()
    {
        ProjectSettings.EnableSelectedExpressionPreview = !ProjectSettings.EnableSelectedExpressionPreview;
        InternalEditorUtility.RepaintAllViews();
    }
}
