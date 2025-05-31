using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.animator;

namespace com.aoyon.facetune.platform;

internal interface IPlatformSupport
{
    public bool IsTarget(Transform root);
    public void Initialize(Transform root)
    {
        return;
    }
    public SkinnedMeshRenderer? GetFaceRenderer();
    public void InstallPatternData(BuildContext buildContext, SessionContext context, PatternData patternData, bool disableExistingControl)
    {
        return;
    }
    public IEnumerable<string> GetTrackedBlendShape()
    {
        return new string[] { };
    }

    // ModularAvatarMenuItem
    public string AssignUniqueParameterName(ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        return string.Empty;
    }
    public void AssignParameterName(ModularAvatarMenuItem menuItem, string parameterName)
    {
        return;
    }
    public void AssignParameterValue(ModularAvatarMenuItem menuItem, float value)
    {
        return;
    }
    public void EnsureMenuItemIsToggle(ModularAvatarMenuItem menuItem)
    {
        return;
    }
    public (string?, ParameterCondition?) MenuItemAsCondition(ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        return (null, null);
    }
    public void SetTracks(VirtualState state, Expression expression)
    {
        return;
    }
}