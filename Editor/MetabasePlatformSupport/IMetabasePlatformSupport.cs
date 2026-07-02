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
    public void InstallExpressionProgram(FaceTuneContext buildPassContext, BuildContext buildContext, ExpressionProgram expressionProgram)
    {
        return;
    }
    public IEnumerable<string> GetTrackedBlendShape()
    {
        return new string[] { };
    }

    public DnfCondition ResolveCondition(Condition condition)
    {
        if (condition.Always) return DnfCondition.Always;
        return condition.Cases.Aggregate(
            DnfCondition.Never,
            (current, conditionCase) => current.Or(ResolveConditionCase(conditionCase)));
    }

    public DnfCondition ResolveConditionCase(ConditionCase conditionCase)
    {
        var result = DnfCondition.Always;
        foreach (var handGestureCondition in conditionCase.HandGestureConditions)
        {
            result = result.And(ResolveHandGestureCondition(handGestureCondition));
        }
        foreach (var parameterCondition in conditionCase.ParameterConditions)
        {
            result = result.And(ResolveParameterCondition(parameterCondition));
        }
        foreach (var menuCondition in conditionCase.MenuConditions)
        {
            result = result.And(ResolveMenuCondition(menuCondition));
        }
        return result;
    }

    public DnfCondition ResolveHandGestureCondition(HandGestureCondition condition)
    {
        throw new NotSupportedException("Hand gesture condition is not supported by this platform");
    }

    public DnfCondition ResolveParameterCondition(ParameterCondition condition)
    {
        throw new NotSupportedException("Parameter condition is not supported by this platform");
    }

    public DnfCondition ResolveMenuCondition(MenuCondition condition)
    {
        throw new NotSupportedException("Menu condition is not supported by this platform");
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
