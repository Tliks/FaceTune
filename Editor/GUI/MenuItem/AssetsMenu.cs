using UnityEngine.SceneManagement;
using nadena.dev.modular_avatar.core;
using M = UnityEditor.MenuItem;

namespace aoyon.facetune.gui;

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

        var uniqueParameterId = $"{FaceTuneConsts.Name}/ExclusiveMenu/{Guid.NewGuid()}";
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

            toggle.AddComponent<ExpressionComponent>();
            var dataComponent = toggle.AddComponent<FacialDataComponent>();
            dataComponent.SourceMode = AnimationSourceMode.AnimationClip;
            dataComponent.Clip = clip;
            dataComponent.ClipOption = ClipImportOption.NonZero;
        }

        menuObject.AddComponent<PatternComponent>();

        SceneManager.MoveGameObjectToScene(menuObject, SceneManager.GetActiveScene());
        Selection.activeGameObject = menuObject;

        Undo.RegisterCreatedObjectUndo(menuObject, "Create Exclusive Menu");
    }
}