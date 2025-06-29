using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using com.aoyon.facetune.build;

namespace com.aoyon.facetune.platform;

internal interface IPlatformSupport
{
    public bool IsTarget(Transform root);
    public void Initialize(Transform root)
    {
        return;
    }
    public SkinnedMeshRenderer? GetFaceRenderer();
    public void DisableExistingControlAndInstallPatternData(BuildPassContext buildPassContext, BuildContext buildContext, InstallData installData)
    {
        return;
    }
    public IEnumerable<string> GetTrackedBlendShape()
    {
        return new string[] { };
    }

    // ModularAvatarMenuItem
    public MenuItemType GetMenuItemType(ModularAvatarMenuItem menuItem)
    {
        return MenuItemType.Button;
    }
    public void SetMenuItemType(ModularAvatarMenuItem menuItem, MenuItemType type)
    {
        return;
    }
    public string GetParameterName(ModularAvatarMenuItem menuItem)
    {
        return string.Empty;
    }
    public string GetUniqueParameterName(ModularAvatarMenuItem menuItem, HashSet<string> usedNames, string suffix)
    {
        return Guid.NewGuid().ToString();
    }
    public void SetParameterName(ModularAvatarMenuItem menuItem, string parameterName)
    {
        return;
    }
    public string GetRadialParameterName(ModularAvatarMenuItem menuItem)
    {
        return string.Empty;
    }
    public void SetRadialParameterName(ModularAvatarMenuItem menuItem, string parameterName)
    {
        return;
    }
    public float GetParameterValue(ModularAvatarMenuItem menuItem)
    {
        return 0;
    }
    public void SetParameterValue(ModularAvatarMenuItem menuItem, float value)
    {
        return;
    }

    public void SetEyeBlinkTrack(VirtualState state, bool isTracking)
    {
        return;
    }
    public void SetLipSyncTrack(VirtualState state, bool isTracking)
    {
        return;
    }
}


internal enum MenuItemType
{
    Button,
    Toggle,
    SubMenu,
    TwoAxisPuppet,
    FourAxisPuppet,
    RadialPuppet,
}