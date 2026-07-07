using UnityEditor.Animations;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal class LipSyncInstaller : InstallerBase
{
    private readonly string _forceDisableLipSyncParameter;
    private const float CancelerThreshold = 0.01f;

    public LipSyncInstaller(
        VirtualAnimatorController virtualController,
        AvatarContext avatarContext,
        bool useWriteDefaults,
        IAnimatorPlatformServices platformServices,
        string forceDisableLipSyncParameter) : base(virtualController, avatarContext, useWriteDefaults, platformServices)
    {
        _forceDisableLipSyncParameter = forceDisableLipSyncParameter;
        if (!string.IsNullOrWhiteSpace(_forceDisableLipSyncParameter))
        {
            _controller.EnsureBoolParameterExists(_forceDisableLipSyncParameter);
        }
    }

    public void AddLipSyncLayer(OutputUnit unit, RuntimeDomain<LipSyncRuntimeMode> domain)
    {
        var localEntries = domain.LocalEntries(unit).ToArray();
        if (localEntries.Length == 0) return;

        var layer = AddLayer($"{unit.Anchor.name} LipSync", LayerPriority);
        var position = EntryStatePosition;

        var foreign = AddTrackingState(layer, "Foreign/Inert", null, position);
        position.y += PositionYStep;
        var baseline = AddTrackingState(layer, "BaselineTracking", true, position);
        position.y += PositionYStep;
        var tracking = AddTrackingState(layer, "Tracking", true, position);
        position.y += PositionYStep;
        var disabled = AddTrackingState(layer, "Disabled", false, position);
        position.y += 2 * PositionYStep;

        var targets = new List<(VirtualState state, IEnumerable<AnimatorCondition> conditions)>
        {
            (baseline, VRCAAPHelper.IndexConditions(domain.ParameterName, true, domain.Baseline.Index))
        };
        targets.AddRange(domain.ForeignEntries(unit)
            .Select(entry => (foreign, VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index))));

        foreach (var entry in localEntries)
        {
            switch (entry.Mode.Kind)
            {
                case LipSyncRuntimeModeKind.Tracking:
                    targets.Add((tracking, VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index)));
                    break;
                case LipSyncRuntimeModeKind.Disabled:
                    targets.Add((disabled, VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index)));
                    break;
                case LipSyncRuntimeModeKind.Canceler:
                    var canceler = AddCancelerStates(layer, domain, entry, entry.Mode.Settings!, position);
                    targets.Add((canceler, VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index)));
                    position.y += 2 * PositionYStep;
                    break;
            }
        }

        ApplyForceDisableGuard(targets, disabled);

        AddEntryTransitions(layer, targets);
        foreach (var state in new[] { foreign, baseline, tracking, disabled }.Concat(targets.Select(target => target.state)).Distinct())
        {
            AddModeTransitions(state, targets.Where(target => target.state != state));
            AddForceDisableTransition(state, disabled);
        }
    }

    private VirtualState AddTrackingState(VirtualLayer layer, string name, bool? tracking, Vector3 position)
    {
        var state = AddState(layer, name, position);
        state.Motion = _emptyClip;
        if (tracking != null)
        {
            _platformServices.SetLipSyncTracking(state, tracking.Value);
        }
        return state;
    }

    private VirtualState AddCancelerStates(
        VirtualLayer layer,
        RuntimeDomain<LipSyncRuntimeMode> domain,
        ModeEntry<LipSyncRuntimeMode> entry,
        AdvancedLipSyncSettings settings,
        Vector3 position)
    {
        var voiceParam = "Voice";
        _controller.EnsureFloatParameterExists(voiceParam);

        var inactive = AddTrackingState(layer, $"Canceler {entry.Index} Inactive", true, position);
        var active = AddTrackingState(layer, $"Canceler {entry.Index} Active", true, position + new Vector3(PositionXStep, 0, 0));
        var cancelerAnimation = settings.CancelerBlendShapeNames.Select(name => BlendShapeWeightAnimation.SingleFrame(name, 0f));
        AddBlendShapeAnimationsToState(active, cancelerAnimation);

        var modeConditions = VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index).ToArray();
        var toActive = AnimatorHelper.CreateTransitionWithDurationSeconds(settings.CancelerEntryDurationSeconds);
        toActive.SetDestination(active);
        toActive.Conditions = modeConditions.Append(new AnimatorCondition
        {
            parameter = voiceParam,
            mode = AnimatorConditionMode.Greater,
            threshold = CancelerThreshold
        }).ToImmutableList();
        inactive.Transitions = inactive.Transitions.Add(toActive);

        var toInactive = AnimatorHelper.CreateTransitionWithDurationSeconds(settings.CancelerExitDurationSeconds);
        toInactive.SetDestination(inactive);
        var orConditions = new List<AnimatorCondition>
        {
            new AnimatorCondition
            {
                parameter = voiceParam,
                mode = AnimatorConditionMode.Less,
                threshold = CancelerThreshold
            }
        };
        if (!string.IsNullOrWhiteSpace(_forceDisableLipSyncParameter))
        {
            orConditions.Add(new AnimatorCondition { parameter = _forceDisableLipSyncParameter, mode = AnimatorConditionMode.If });
        }
        orConditions.AddRange(VRCAAPHelper.IndexConditions(domain.ParameterName, false, entry.Index));
        active.Transitions = active.Transitions.AddRange(AnimatorHelper.SetORConditions(toInactive, orConditions));

        return inactive;
    }

    private static void AddEntryTransitions(
        VirtualLayer layer,
        IEnumerable<(VirtualState state, IEnumerable<AnimatorCondition> conditions)> targets)
    {
        var transitions = targets.Select(target =>
        {
            var transition = VirtualTransition.Create();
            transition.SetDestination(target.state);
            transition.Conditions = target.conditions.ToImmutableList();
            return transition;
        });
        layer.StateMachine!.EntryTransitions = layer.StateMachine!.EntryTransitions.AddRange(transitions);
    }

    private static void AddModeTransitions(
        VirtualState source,
        IEnumerable<(VirtualState state, IEnumerable<AnimatorCondition> conditions)> targets)
    {
        foreach (var target in targets)
        {
            var transition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            transition.SetDestination(target.state);
            transition.Conditions = target.conditions.ToImmutableList();
            source.Transitions = source.Transitions.Add(transition);
        }
    }

    private void ApplyForceDisableGuard(List<(VirtualState state, IEnumerable<AnimatorCondition> conditions)> targets, VirtualState disabled)
    {
        if (string.IsNullOrWhiteSpace(_forceDisableLipSyncParameter)) return;

        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            if (target.state == disabled) continue;
            targets[index] = (target.state, target.conditions.Append(new AnimatorCondition
            {
                parameter = _forceDisableLipSyncParameter,
                mode = AnimatorConditionMode.IfNot
            }).ToArray());
        }

        targets.Add((disabled, new[]
        {
            new AnimatorCondition { parameter = _forceDisableLipSyncParameter, mode = AnimatorConditionMode.If }
        }));
    }

    private void AddForceDisableTransition(VirtualState source, VirtualState disabled)
    {
        if (string.IsNullOrWhiteSpace(_forceDisableLipSyncParameter) || source == disabled) return;
        var transition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        transition.SetDestination(disabled);
        transition.Conditions = ImmutableList.Create(new AnimatorCondition
        {
            parameter = _forceDisableLipSyncParameter,
            mode = AnimatorConditionMode.If
        });
        source.Transitions = source.Transitions.Add(transition);
    }
}
