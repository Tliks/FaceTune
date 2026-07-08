using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

internal interface IAnimatorPlatformServices
{
    void SetEyeBlinkTracking(VirtualState state, bool isTracking);
    void SetLipSyncTracking(VirtualState state, bool isTracking);
    void AddRandomDriver(VirtualState state, string parameterName, float min, float max);

    int MaxAapIndex { get; }
    float EncodeAapIndex(int index);
    IEnumerable<AnimatorCondition> AapIndexConditions(string parameterName, bool equal, int index);

    bool IsUnitBoundaryTransform(
        Transform transform,
        VirtualControllerContext controllerContext,
        ISet<string> managedBlendShapeNames)
    {
        return false;
    }
}
