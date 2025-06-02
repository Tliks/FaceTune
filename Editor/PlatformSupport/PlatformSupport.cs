using nadena.dev.ndmf;
using nadena.dev.ndmf.runtime;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.animator;
using com.aoyon.facetune.pass;

namespace com.aoyon.facetune.platform;

internal static class PlatformSupport
{
    private static readonly List<IPlatformSupport> s_supports = new();
    private static readonly IPlatformSupport s_fallback = new FallbackSupport();
    
    public static void Register(IPlatformSupport support)
    {
        s_supports.Add(support);
    }

    public static Transform? FindAvatarInParents(Transform transform)
    {
        return RuntimeUtil.FindAvatarInParents(transform); // NDMFが対応する範囲が上限
    }

    public static IEnumerable<IPlatformSupport> GetSupports(Transform root)
    {
        foreach (var support in s_supports)
        {
            if (support.IsTarget(root))
            {
                support.Initialize(root);
                yield return support;
            }
        }
        s_fallback.Initialize(root);
        yield return s_fallback;
    }

    public static SkinnedMeshRenderer? GetFaceRenderer(Transform root)
    {
        return GetSupports(root).Select(s => s.GetFaceRenderer()).FirstOrNull(r => r != null);
    }

    public static void DisableExistingControl(FTPassContext passContext)
    {
        GetSupports(passContext.BuildContext.AvatarRootObject.transform).First().DisableExistingControl(passContext);
    }

    public static void InstallPatternData(FTPassContext passContext, PatternData patternData)
    {
        GetSupports(passContext.BuildContext.AvatarRootObject.transform).First().InstallPatternData(passContext, patternData);
    }

    public static IEnumerable<string> GetTrackedBlendShape(Transform root)
    {
        return GetSupports(root).First().GetTrackedBlendShape();
    }

    // ModularAvatarMenuItem
    public static string AssignUniqueParameterName(Transform root, ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        return GetSupports(root).First().AssignUniqueParameterName(menuItem, usedNames);
    }
    public static void AssignParameterName(Transform root, ModularAvatarMenuItem menuItem, string parameterName)
    {
        GetSupports(root).First().AssignParameterName(menuItem, parameterName);
    }
    public static void AssignParameterValue(Transform root, ModularAvatarMenuItem menuItem, float value)
    {
        GetSupports(root).First().AssignParameterValue(menuItem, value);
    }
    public static void EnsureMenuItemIsToggle(Transform root, ModularAvatarMenuItem menuItem)
    {
        GetSupports(root).First().EnsureMenuItemIsToggle(menuItem);
    }
    public static (string?, ParameterCondition?) MenuItemAsCondition(Transform root, ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        return GetSupports(root).First().MenuItemAsCondition(menuItem, usedNames);
    }

    public static void SetTracks(Transform root,VirtualState state, Expression expression)
    {
        GetSupports(root).First().SetTracks(state, expression);
    }
}
