using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

internal sealed class AnimatorInstaller
{
    private const int InitialPriority = -1;
    private const int UnitPriority = 0;
    private const int TrackingControlPriority = 0;

    private static readonly Vector3 EntryStatePosition = new(300, 0, 0);
    private const float PositionYStep = 50;

    private readonly VirtualControllerContext _controllerContext;
    private readonly AvatarContext _avatarContext;
    private readonly IAnimatorPlatformServices _platformServices;
    private readonly AnimatorBuildPlan _plan;
    private readonly bool _useWriteDefaults;
    private readonly VirtualClip _emptyClip;

    public AnimatorInstaller(
        VirtualControllerContext controllerContext,
        AvatarContext avatarContext,
        bool useWriteDefaults,
        IAnimatorPlatformServices platformServices,
        AnimatorBuildPlan plan)
    {
        _controllerContext = controllerContext;
        _avatarContext = avatarContext;
        _useWriteDefaults = useWriteDefaults;
        _platformServices = platformServices;
        _plan = plan;
        _emptyClip = AnimatorHelper.CreateCustomEmptyClip();
    }

    public void Execute()
    {
        if (_plan.Units.Count == 0) return;

        InstallInitialController();
        foreach (var unit in _plan.Units)
        {
            InstallUnitController(unit);
        }

        if (_plan.TrackingControlLayer != null)
        {
            InstallTrackingControlController(_plan.TrackingControlLayer);
        }
    }

    private void InstallInitialController()
    {
        var controller = _platformServices.CreateController(_controllerContext, _plan.InitialLayer.Anchor, _plan.InitialLayer.Name, InitialPriority);
        var layer = AddLayer(controller, _plan.InitialLayer.Name, InitialPriority);

        var state = AddState(layer, "Default", EntryStatePosition);
        layer.StateMachine!.DefaultState = state;
        var clip = state.SetNewClip("Initial");
        clip.AddBlendShapeAnimations(_avatarContext.BodyPath, _plan.InitialLayer.BlendShapes.ToBlendShapeAnimations());
    }

    private void InstallUnitController(OutputUnitPlan unit)
    {
        var controller = _platformServices.CreateController(_controllerContext, unit.Anchor, $"Unit {unit.Id}", UnitPriority);
        foreach (var param in unit.Parameters)
            controller.EnsureParameterExists(param.Type, param.Name, param.DefaultValue);
        foreach (var layer in unit.ExpressionLayers)
        {
            InstallExpressionLayer(controller, layer);
        }

        if (unit.AdvancedEyeBlink != null)
        {
            InstallEmptyAdvancedLayer(controller, unit.AdvancedEyeBlink.Name, unit.AdvancedEyeBlink.ForceInactiveWhen);
        }
        if (unit.AdvancedLipSync != null)
        {
            InstallEmptyAdvancedLayer(controller, unit.AdvancedLipSync.Name, unit.AdvancedLipSync.ForceInactiveWhen);
        }
    }

    private void InstallTrackingControlController(TrackingControlLayerPlan trackingControl)
    {
        var controller = _platformServices.CreateController(_controllerContext, trackingControl.Anchor, trackingControl.Name, TrackingControlPriority);
        foreach (var param in trackingControl.Parameters)
            controller.EnsureParameterExists(param.Type, param.Name, param.DefaultValue);
        InstallTrackingControlLayer(controller, trackingControl);
    }

    private void InstallExpressionLayer(VirtualAnimatorController controller, ExpressionLayerPlan plan)
    {
        var layer = AddLayer(controller, plan.Name, UnitPriority);

        var defaultState = AddState(layer, "PassThrough", EntryStatePosition);
        AsPassThrough(defaultState);
        layer.StateMachine!.DefaultState = defaultState;
        SetExitTransitions(defaultState, plan.DefaultExitWhen, _plan.ExpressionTransitionDurationSeconds);

        var position = EntryStatePosition + new Vector3(0, PositionYStep * 2, 0);
        foreach (var statePlan in plan.States)
        {
            var state = AddState(layer, statePlan.Name, position);
            position.y += Math.Max(1, statePlan.EnterWhen.Cases.Count) * PositionYStep;
            SetExpressionClip(state, statePlan);
            AddEntryTransition(layer, state, statePlan.EnterWhen);
            SetExitTransitions(state, statePlan.ExitWhen, _plan.ExpressionTransitionDurationSeconds);
        }

        AddForceInactiveState(layer, "Disabled", plan.ForceInactiveWhen, true);
    }

    private void InstallEmptyAdvancedLayer(VirtualAnimatorController controller, string name, DnfCondition? forceInactiveWhen)
    {
        var layer = AddLayer(controller, name, UnitPriority);
        
        AddForceInactiveState(layer, "Disabled", forceInactiveWhen, true);
    }

    private void InstallTrackingControlLayer(VirtualAnimatorController controller, TrackingControlLayerPlan trackingControl)
    {
        var layer = AddLayer(controller, trackingControl.Name, TrackingControlPriority);

        var defaultState = AddState(layer, trackingControl.DefaultState.Name, EntryStatePosition);
        SetTrackingControlState(defaultState, trackingControl.DefaultState);
        layer.StateMachine!.DefaultState = defaultState;
        SetExitTransitions(defaultState, trackingControl.DefaultExitWhen, _plan.ExpressionTransitionDurationSeconds);

        var position = EntryStatePosition + new Vector3(0, PositionYStep * 2, 0);
        foreach (var statePlan in trackingControl.States)
        {
            var state = AddState(layer, statePlan.Name, position);
            position.y += PositionYStep;
            SetTrackingControlState(state, statePlan);
            AddEntryTransition(layer, state, statePlan.When);
            SetExitTransitions(state, statePlan.When.Not(), _plan.ExpressionTransitionDurationSeconds);
        }

        AddForceInactiveState(layer, "Disabled", trackingControl.ForceInactiveWhen, false);
    }

    private void SetExpressionClip(VirtualState state, ExpressionStatePlan plan)
    {
        var clip = state.SetNewClip(plan.Name);

        clip.AddBlendShapeAnimations(_avatarContext.BodyPath, plan.Animations);

        foreach (var write in plan.AapWrites)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, write.Value);
            clip.SetFloatCurve("", typeof(UnityEngine.Animator), write.ParameterName, curve);
        }

        var settings = plan.Settings;
        if (settings.LoopTime)
        {
            var clipSettings = clip.Settings;
            clipSettings.loopTime = true;
            clip.Settings = clipSettings;
        }
        else if (!string.IsNullOrEmpty(settings.MotionTimeParameterName))
        {
            state.TimeParameter = settings.MotionTimeParameterName;
        }
    }

    private void SetTrackingControlState(VirtualState state, TrackingControlStatePlan plan)
    {
        state.Motion = _emptyClip;
        if (plan.EyeBlinkTracking != null)
        {
            _platformServices.SetEyeBlinkTracking(state, plan.EyeBlinkTracking.Value);
        }
        if (plan.LipSyncTracking != null)
        {
            _platformServices.SetLipSyncTracking(state, plan.LipSyncTracking.Value);
        }
    }


    private void AddForceInactiveState(VirtualLayer layer, string name, DnfCondition? forceInactiveWhen, bool passThrough)
    {
        if (forceInactiveWhen == null) return;

        var state = AddState(layer, name, EntryStatePosition + new Vector3(0, PositionYStep, 0));
        if (passThrough)
        {
            AsPassThrough(state);
        }
        else
        {
            state.Motion = _emptyClip;
        }

        SetAnyStateTransition(layer, state, forceInactiveWhen, 0f);
        SetExitTransitions(state, forceInactiveWhen.Not(), 0f);
    }

    private VirtualLayer AddLayer(VirtualAnimatorController controller, string name, int priority)
    {
        return controller.AddLayer(new LayerPriority(priority), $"{FaceTuneConstants.Name}: {name}");
    }

    private VirtualState AddState(VirtualLayer layer, string name, Vector3 position)
    {
        var state = layer.StateMachine!.AddState(name, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        return state;
    }

    private void AsPassThrough(VirtualState state)
    {
        state.Motion = _useWriteDefaults ? null : _emptyClip;
    }

    private void SetTransition(VirtualState source, VirtualState destination, DnfCondition when, float duration)
    {
        var transitions = ImmutableList.CreateBuilder<VirtualStateTransition>();
        foreach (var conditionCase in when.Cases)
        {
            var transition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
            transition.SetDestination(destination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            transitions.Add(transition);
        }
        source.Transitions = transitions.ToImmutable();
    }

    private void SetExitTransitions(VirtualState state, DnfCondition when, float duration)
    {
        var transitions = ImmutableList.CreateBuilder<VirtualStateTransition>();
        foreach (var conditionCase in when.Cases)
        {
            var transition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
            transition.SetExitDestination();
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            transitions.Add(transition);
        }
        state.Transitions = transitions.ToImmutable();
    }

    private void SetAnyStateTransition(VirtualLayer layer, VirtualState destination, DnfCondition when, float duration)
    {
        var transitions = ImmutableList.CreateBuilder<VirtualStateTransition>();
        foreach (var conditionCase in when.Cases)
        {
            var transition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
            transition.SetDestination(destination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            transitions.Add(transition);
        }
        layer.StateMachine!.AnyStateTransitions = transitions.ToImmutable();
    }

    private void AddEntryTransition(VirtualLayer layer, VirtualState destination, DnfCondition when)
    {
        var transitions = new List<VirtualTransition>();
        foreach (var conditionCase in when.Cases)
        {
            var transition = VirtualTransition.Create();
            transition.SetDestination(destination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            transitions.Add(transition);
        }
        layer.StateMachine!.EntryTransitions = layer.StateMachine!.EntryTransitions.AddRange(transitions);
    }


    private IEnumerable<AnimatorCondition> ToAnimatorConditions(DnfCase conditionCase)
    {
        return conditionCase.Rules.Select(ToAnimatorCondition);
    }

    private AnimatorCondition ToAnimatorCondition(DnfRule rule)
    {
        var animatorConditionRule = (AnimatorConditionRule)rule;
        return animatorConditionRule.Condition;
    }
}
