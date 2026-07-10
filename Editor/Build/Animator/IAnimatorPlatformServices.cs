using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal interface IAnimatorPlatformServices
{
    DiscreteFloatParameterRange FloatRange { get; }

    void SetEyeBlinkTracking(VirtualState state, bool isTracking);
    void SetLipSyncTracking(VirtualState state, bool isTracking);
    void AddRandomDriver(VirtualState state, string parameterName, float min, float max);

    bool IsUnitBoundaryTransform(
        Transform transform,
        VirtualControllerContext controllerContext,
        ISet<string> managedBlendShapeNames);

    VirtualAnimatorController CreateController(
        VirtualControllerContext controllerContext,
        Transform anchor,
        string name,
        int priority);
}
