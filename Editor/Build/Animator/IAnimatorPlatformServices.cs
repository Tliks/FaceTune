using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal interface IAnimatorPlatformServices
{
    DiscreteFloatParameterRange FloatRange { get; }

    void SetEyeBlinkTracking(VirtualState state, bool isTracking);
    void SetLipSyncTracking(VirtualState state, bool isTracking);
    void AddRandomDriver(VirtualState state, string parameterName, float min, float max);

}
