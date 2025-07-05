using UnityEngine.SceneManagement;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.ScriptableObjects;
using M = UnityEditor.MenuItem;

namespace aoyon.facetune.ui;

internal static class AssetsMenu
{
    private const string BasePath = $"Assets/{FaceTuneConsts.Name}/";

    private const string Assets_SelectedClipsToExclusiveMenuPath = BasePath + "SelectedClipsToExclusiveMenu";


    [M(Assets_SelectedClipsToExclusiveMenuPath, true)]
    private static bool ValidateSelectedClipsToExclusiveMenu()
    {
        var clips = Selection.objects.OfType<AnimationClip>();
        return clips.Count() >= 2;
    }

    [M(Assets_SelectedClipsToExclusiveMenuPath, false)]
    private static void SelectedClipsToExclusiveMenu()
    {
        GenerateExclusiveMenuFromClips(Selection.objects.OfType<AnimationClip>().ToArray());
    }

    private static void GenerateExclusiveMenuFromClips(AnimationClip[] clips)
    {
        var menuName = "ExclusiveMenu";
        var menuObject = new GameObject(menuName);
        var subMenu = menuObject.AddComponent<ModularAvatarMenuItem>();
        subMenu.Control.name = menuName;
        subMenu.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
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
            toggleComponent.Control.name = clip.name;
            toggleComponent.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            toggleComponent.Control.parameter = new() { name = uniqueParameterId };
            toggleComponent.Control.value = i;

            toggle.AddComponent<ExpressionComponent>();
            var dataComponent = toggle.AddComponent<FacialDataComponent>();
            dataComponent.SourceMode = AnimationSourceMode.AnimationClip;
            dataComponent.Clip = clip;
            dataComponent.ClipExcludeOption = ClipExcludeOption.ExcludeZeroWeight;
        }

        menuObject.AddComponent<PatternComponent>();

        SceneManager.MoveGameObjectToScene(menuObject, SceneManager.GetActiveScene());
        Selection.activeGameObject = menuObject;

        Undo.RegisterCreatedObjectUndo(menuObject, "Create Exclusive Menu");
    }
}