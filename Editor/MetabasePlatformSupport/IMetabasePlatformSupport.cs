using Aoyon.FaceTune.Build;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms;

internal interface IMetabasePlatformSupport
{
    IPlatformBuildBackend? BuildBackend => null;

    SkinnedMeshRenderer? GetFaceRenderer();

    IEnumerable<string> GetExternallyControlledBlendShapeNames();

    ParameterDomainRegistry CreateBuiltInParameterDomains();

    DnfCondition? ResolveHandGestureCondition(HandGestureCondition condition);

    DnfCondition? ResolveParameterCondition(ParameterCondition condition);

    string? ResolveGestureWeightParameter(Hand hand);

    AnimatorController? GetAnimatorController()
    {
        return null;
    }
}
