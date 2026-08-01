using Aoyon.FaceTune.Build;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms;

internal interface IMetabasePlatformSupport
{
    IPlatformBuildBackend? BuildBackend => null;

    SkinnedMeshRenderer? GetFaceRenderer();

    IEnumerable<string> GetExternallyControlledBlendShapeNames();

    MmdPlaybackSettings ResolveMmdPlaybackSettings()
    {
        return MmdPlaybackSettings.Disabled;
    }

    void PostProcessDefaultBlendShapes(BuildSettings settings, BlendShapeWeightSet blendShapes)
    {
    }

    IEnumerable<GameObject> GetMenuFolderObjects()
    {
        return Array.Empty<GameObject>();
    }

    ParameterDomainRegistry CreateBuiltInParameterDomains();

    DnfCondition? ResolveHandGestureCondition(
        HandGestureCondition condition,
        ParameterDomainRegistry parameterDomains);

    DnfCondition? ResolveParameterCondition(
        ParameterCondition condition,
        ParameterDomainRegistry parameterDomains);

    string? ResolveGestureParameter(Hand hand);

    string? ResolveGestureWeightParameter(Hand hand);

    AnimatorController? GetAnimatorController()
    {
        return null;
    }
}
