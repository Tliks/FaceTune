using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal interface IAnimatorPlatformServices
{
    void SetEyeBlinkTracking(VirtualState state, bool isTracking);
    void SetLipSyncTracking(VirtualState state, bool isTracking);
    void AddRandomDriver(VirtualState state, string parameterName, float min, float max);

    DiscreteFloatParameterRange AapFloatRange { get; }

    bool IsUnitBoundaryTransform(
        Transform transform,
        VirtualControllerContext controllerContext,
        ISet<string> managedBlendShapeNames)
    {
        return false;
    }
}
