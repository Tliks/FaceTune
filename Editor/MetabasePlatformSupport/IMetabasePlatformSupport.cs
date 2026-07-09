using nadena.dev.ndmf;
using Aoyon.FaceTune.Build;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms;

internal interface IMetabasePlatformSupport
{
    public bool IsTarget(Transform root);
    public void Initialize(Transform root)
    {
        return;
    }
    public SkinnedMeshRenderer? GetFaceRenderer();
    public void InstallBuild(BuildContext buildContext, BuildSettings settings, ExpressionProgram expressionProgram)
    {
        return;
    }
    public IEnumerable<string> GetExternallyControlledBlendShapeNames()
    {
        return Array.Empty<string>();
    }

    public ParameterDomainRegistry CreateBuiltInParameterDomains()
    {
        return new ParameterDomainRegistry();
    }

    public DnfCondition ResolveHandGestureCondition(HandGestureCondition condition)
    {
        throw new NotSupportedException("Hand gesture condition is not supported by this platform");
    }

    public DnfCondition ResolveParameterCondition(ParameterCondition condition)
    {
        throw new NotSupportedException("Parameter condition is not supported by this platform");
    }


    public AnimatorController? GetAnimatorController()
    {
        return null;
    }
}
