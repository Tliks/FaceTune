using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
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
    public void InstallPatternData(BuildPassContext buildPassContext, BuildContext buildContext, InstallerData installerData)
    {
        return;
    }
    public IEnumerable<string> GetTrackedBlendShape()
    {
        return new string[] { };
    }


    public void SetEyeBlinkTrack(VirtualState state, bool isTracking)
    {
        return;
    }
    public void SetLipSyncTrack(VirtualState state, bool isTracking)
    {
        return;
    }
    public void StateAsRandrom(VirtualState state, string parameterName, float min, float max)
    {
        return;
    }
    public (TrackingPermission eye, TrackingPermission mouth)? GetTrackingPermission(AnimatorState state)
    {
        return null;
    }

    public AnimatorController? GetAnimatorController()
    {
        return null;
    }
}
