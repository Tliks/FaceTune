namespace Aoyon.FaceTune.Gui;

internal static class MenuItems
{
    // Tools
    private const string ToolsPath = $"Tools/{FaceTuneConstants.Name}/";
    
    public const string FacialShapesEditorPath = ToolsPath + "Facial Shapes Editor";
    public const int FacialShapesEditorPriority = 1000;

    private const string ToolsSettingsPath = ToolsPath + "Settings/";
    public const string SelectedExpressionPreviewPath = ToolsSettingsPath + "Selected Expression Preview";
    public const int SelectedExpressionPreviewPriority = 1100;

    private const string ToolsDebugPath = ToolsPath + "Debug/";
    public const string ReloadLocalizationPath = ToolsDebugPath + "Reload Localization";
    public const int ReloadLocalizationPriority = 1200;

    // Assets
    private const string AssetsPath = $"Assets/{FaceTuneConstants.Name}/";

    public const string EditAnimationClipMenuPath = AssetsPath + "Edit Animation Clip";
    public const int EditAnimationClipMenuPriority = 1000;

    public const string SelectedClipsToExclusiveMenuPath = AssetsPath + "SelectedClipsToExclusiveMenu";
    public const int SelectedClipsToExclusiveMenuPriority = 1001;


    // GameObject
    private const string GameObjectPath = $"GameObject/{FaceTuneConstants.Name}/";

    public const string BaseTemplatePath = GameObjectPath + "Base Template";
    public const int BaseTemplatePriority = 100;

    public const string ConditionPath = GameObjectPath + "Condition";
    public const int ConditionPriority = 200;

    public const string MenuSinglePath = GameObjectPath + "Menu/Single";
    public const int MenuSinglePriority = 201;

    public const string MenuExclusivePath = GameObjectPath + "Menu/Exclusive";
    public const int MenuExclusivePriority = 202;

    public const string MenuBlendingPath = GameObjectPath + "Menu/Blending";
    public const int MenuBlendingPriority = 203;

    private const string DebugPath = GameObjectPath + "Debug/";

    public const string DebugModifyHierarchyPassPath = DebugPath + "Modify Hierarchy Pass";
    public const int DebugModifyHierarchyPassPriority = 300;
}
