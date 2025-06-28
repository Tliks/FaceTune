using UnityEditorInternal;
using com.aoyon.facetune.Settings;

namespace com.aoyon.facetune.ui;

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

    private const string Tools_ImportFromVRChatFXPath = BasePath + "ImportFromVRChatFX";

    [MenuItem(Tools_ImportFromVRChatFXPath, false)]
    private static void ImportFromVRChatFX()
    {
        var animatorController = Selection.activeObject as UnityEditor.Animations.AnimatorController;
        if (animatorController == null)
        {
            Debug.LogError("AnimatorControllerを選択してください");
            return;
        }
        FXImporter.ImportFromVRChatFX(animatorController);
    }
}
