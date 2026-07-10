using Aoyon.FaceTune.Build;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms;

internal interface IMetabasePlatformSupport
{
    IPlatformBuildBackend? BuildBackend => null;

    SkinnedMeshRenderer? GetFaceRenderer();

    IEnumerable<string> GetExternallyControlledBlendShapeNames()
    {
        return Array.Empty<string>();
    }

    ParameterDomainRegistry CreateBuiltInParameterDomains()
    {
        return new ParameterDomainRegistry();
    }

    DnfCondition ResolveHandGestureCondition(HandGestureCondition condition)
    {
        throw new NotSupportedException("Hand gesture condition is not supported by this platform");
    }

    DnfCondition ResolveParameterCondition(ParameterCondition condition)
    {
        throw new NotSupportedException("Parameter condition is not supported by this platform");
    }

    AnimatorController? GetAnimatorController()
    {
        return null;
    }
}
