using UnityEngine.SceneManagement;
using M = UnityEditor.MenuItem;
using Aoyon.FaceTune.Importer;
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

    private const string ToolsDebugPath = ToolsPath + "Debug/";
    public const string ReloadLocalizationPath = ToolsDebugPath + "Reload Localization";
    public const int ReloadLocalizationPriority = 1200;

    // Assets
    private const string AssetsPath = "Assets/" + FaceTuneConstants.Name + "/";

    public const string EditAnimationClipMenuPath = AssetsPath + "Edit Animation Clip";
    public const int EditAnimationClipMenuPriority = 1000;

    public const string SelectedClipsToExclusiveMenuPath = AssetsPath + "SelectedClipsToExclusiveMenu";
    public const int SelectedClipsToExclusiveMenuPriority = 1001;


    // GameObject
    private const string GameObjectPath = "GameObject/" + FaceTuneConstants.Name + "/";

    public const string TemplatePath = GameObjectPath + "Template";
    public const int TemplatePriority = 100;

    public const string ImportFxPath = GameObjectPath + "Import FX";
    public const int ImportFxPriority = 101;

    public const string ConditionPath = GameObjectPath + "Condition";
    public const int ConditionPriority = 200;

    public const string MenuSinglePath = GameObjectPath + "Menu/Single";
    public const int MenuSinglePriority = 201;

    public const string MenuExclusivePath = GameObjectPath + "Menu/Exclusive";
    public const int MenuExclusivePriority = 202;

    public const string MenuBlendingPath = GameObjectPath + "Menu/Blending";
    public const int MenuBlendingPriority = 203;

    private const string DebugPath = GameObjectPath + "Debug/";

}

#if false // Temporarily disabled: depends on Modular Avatar editor functionality.
internal static class AssetsMenu
{


    [M(MenuItems.SelectedClipsToExclusiveMenuPath, true)]
    private static bool ValidateSelectedClipsToExclusiveMenu()
    {
        var clips = Selection.objects.OfType<AnimationClip>();
        return clips.Count() >= 2;
    }

    [M(MenuItems.SelectedClipsToExclusiveMenuPath, false, MenuItems.SelectedClipsToExclusiveMenuPriority)]
    private static void SelectedClipsToExclusiveMenu()
    {
        GenerateExclusiveMenuFromClips(Selection.objects.OfType<AnimationClip>().ToArray());
    }

    private static void GenerateExclusiveMenuFromClips(AnimationClip[] clips)
    {
        var menuName = "ExclusiveMenu";
        var menuObject = new GameObject(menuName);
        var subMenu = menuObject.AddComponent<ModularAvatarMenuItem>();
        subMenu.PortableControl.Type = PortableControlType.SubMenu;
        subMenu.MenuSource = SubmenuSource.Children;

        var uniqueParameterId = FaceTuneConstants.Name + "/ExclusiveMenu/" + Guid.NewGuid();
        var parameters = menuObject.AddComponent<ModularAvatarParameters>();
        parameters.parameters.Add(new ParameterConfig()
        {
            nameOrPrefix = uniqueParameterId,
            syncType = ParameterSyncType.Int,
            defaultValue = 0,
        });
        
        for (int i = 1; i <= clips.Length; i++)
        {
            var clip = clips[i - 1];
            var toggle = new GameObject(clip.name);
            toggle.transform.SetParent(subMenu.transform);
            var toggleComponent = toggle.AddComponent<ModularAvatarMenuItem>();
            toggleComponent.PortableControl.Type = PortableControlType.Toggle;
            toggleComponent.PortableControl.Parameter = uniqueParameterId;
            toggleComponent.PortableControl.Value = i;

            toggle.AddComponent<FaceTuneComponent>();
            var dataComponent = toggle.AddComponent<DataComponent>();
            dataComponent.Data.Clip = clip;
            dataComponent.Data.ClipOption = ClipImportOption.NonZero;
        }

        menuObject.AddComponent<PatternComponent>();

        SceneManager.MoveGameObjectToScene(menuObject, SceneManager.GetActiveScene());
        Selection.activeGameObject = menuObject;

        Undo.RegisterCreatedObjectUndo(menuObject, "Create Exclusive Menu");
    }
}


#endif

internal static class GameObjectMenu
{
    private static GameObject IP(string guid, bool unpack = true, bool isFirstSibling = false, bool addInstaller = false)
    {
        var parent = Selection.activeGameObject;
        return Utils.InstantiatePrefab(guid, unpack: unpack, parent: parent, isFirstSibling: isFirstSibling, addInstaller: addInstaller);
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
            .Select(support => (Support: support, Controller: support.GetAnimatorController()))
            .Where(candidate => candidate.Controller != null)
            .Select(candidate => (candidate.Support, Controller: candidate.Controller!))
            .ToArray();
        if (candidates.Length != 1) throw new Exception("Failed to uniquely identify an animator controller");

        var (support, animatorController) = candidates[0];
        var importer = new AnimatorControllerImporter(context, animatorController, support);
        importer.Import(selected);
    }

    [M(MenuItems.ConditionPath, false, MenuItems.ConditionPriority)] 
    static void Condition() => IP("20aca02f84d174940bb4ca676555589a");
    
    [M(MenuItems.MenuSinglePath, false, MenuItems.MenuSinglePriority)] 
    static void MenuSingle() => IP("a045ae2cad411ae43b4c008ff814957e", addInstaller: true); // Installerが必要

    [M(MenuItems.MenuExclusivePath, false, MenuItems.MenuExclusivePriority)] 
    static void MenuExclusive() => IP("9e1741e66ac069742976cf8c7e785a35", addInstaller: true); // Installerが必要

    [M(MenuItems.MenuBlendingPath, false, MenuItems.MenuBlendingPriority)] 
    static void MenuBlending() => IP("557c13125870f764bb20173aa14b004f", addInstaller: true); // Installerが必要

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
}
