using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal interface IAnimatorPlatformServices
{
    DnfCondition? GetLayerForceInactiveWhen(BuildSettings settings);
    InitialLayerPlan TransformInitialLayer(InitialLayerPlan initial, BuildSettings settings);

    void SetEyeBlinkTracking(VirtualState state, bool isTracking);
    void SetLipSyncTracking(VirtualState state, bool isTracking);
    void AddRandomDriver(VirtualState state, string parameterName, float min, float max);

}
