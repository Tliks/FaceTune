using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>Virtual Animatorの配置規則とtransition生成を一箇所にまとめる。</summary>
internal sealed class AnimatorGraph
{
    private const string AlwaysParameterName =
        FaceTuneConstants.GeneratedParameterPrefix + "/Always";

    public static readonly Vector3 DefaultStatePosition = new(300, 0, 0);
    public const float PositionXStep = 250f;
    public const float PositionYStep = 50f;

    private readonly bool _useWriteDefaults;
    private readonly VirtualClip _emptyClip;

    public AnimatorGraph(bool useWriteDefaults)
    {
        _useWriteDefaults = useWriteDefaults;
        _emptyClip = AnimatorHelper.CreateCustomEmptyClip();
    }

    public VirtualLayer AddLayer(VirtualAnimatorController controller, string name, int priority)
        => controller.AddLayer(new LayerPriority(priority), $"{FaceTuneConstants.Name}: {name}");

    public VirtualState AddState(VirtualLayer layer, string name, Vector3 position)
    {
        var state = layer.StateMachine!.AddState(name, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        return state;
    }

    public void AsPassThrough(VirtualState state)
    {
        state.Motion = _useWriteDefaults ? null : _emptyClip;
    }

    public static void EnsureAlwaysParameter(VirtualAnimatorController controller)
        => controller.EnsureBoolParameterExists(AlwaysParameterName, true);

    public static void EnsureConditionParameters(
        VirtualAnimatorController controller,
        params DnfCondition?[] conditions)
    {
        foreach (var condition in conditions)
        {
            if (condition == null) continue;
            foreach (var rule in condition.Cases
                         .SelectMany(conditionCase => conditionCase.Rules)
                         .OfType<AnimatorConditionRule>())
            {
                controller.EnsureParameterExists(
                    rule.ParameterType,
                    rule.ParameterName,
                    0f);
            }
        }
    }

    public void AddStateTransition(
        VirtualState source,
        VirtualState destination,
        DnfCondition when,
        float duration)
    {
        var transitions = TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateStateTransition(destination, duration);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        source.Transitions = source.Transitions.AddRange(transitions);
    }

    public void AddExitTimeTransition(
        VirtualState source,
        VirtualState destination,
        DnfCondition when,
        float exitTime,
        float duration)
    {
        var transitions = TransitionCases(when).Select(conditionCase =>
        {
            var transition = AnimatorHelper.CreateTransitionWithExitTime(exitTime, duration);
            transition.SetDestination(destination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        source.Transitions = source.Transitions.AddRange(transitions);
    }

    public void SetExitTransitions(VirtualState state, DnfCondition when, float duration)
    {
        state.Transitions = ImmutableList<VirtualStateTransition>.Empty;
        AddExitTransitions(state, when, duration);
    }

    public void AddExitTransitions(VirtualState state, DnfCondition when, float duration)
    {
        var transitions = TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateExitTransition(duration);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        // 解除条件は時間遷移より先に評価する。
        state.Transitions = transitions.Concat(state.Transitions).ToImmutableList();
    }

    public void SetAnyStateTransition(
        VirtualLayer layer,
        VirtualState destination,
        DnfCondition when,
        float duration)
    {
        var transitions = TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateAnyStateTransition(destination, duration);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        layer.StateMachine!.AnyStateTransitions = transitions.ToImmutableList();
    }

    public void AddEntryTransition(VirtualLayer layer, VirtualState destination, DnfCondition when)
    {
        var transitions = TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateEntryTransition(destination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        layer.StateMachine!.EntryTransitions =
            layer.StateMachine.EntryTransitions.AddRange(transitions);
    }

    private static IEnumerable<DnfCase> TransitionCases(DnfCondition condition)
    {
        if (condition.IsNever) yield break;
        foreach (var conditionCase in condition.Cases) yield return conditionCase;
    }

    private static VirtualStateTransition CreateStateTransition(
        VirtualState destination,
        float duration)
    {
        var transition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
        transition.SetDestination(destination);
        return transition;
    }

    private static VirtualStateTransition CreateExitTransition(float duration)
    {
        var transition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
        transition.SetExitDestination();
        return transition;
    }

    private static VirtualStateTransition CreateAnyStateTransition(
        VirtualState destination,
        float duration)
    {
        var transition = CreateStateTransition(destination, duration);
        transition.CanTransitionToSelf = false;
        return transition;
    }

    private static VirtualTransition CreateEntryTransition(VirtualState destination)
    {
        var transition = VirtualTransition.Create();
        transition.SetDestination(destination);
        return transition;
    }

    private static IEnumerable<AnimatorCondition> ToAnimatorConditions(DnfCase conditionCase)
    {
        if (conditionCase.IsAlways)
        {
            return new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = AlwaysParameterName
                }
            };
        }

        return conditionCase.Rules
            .Cast<AnimatorConditionRule>()
            .OrderBy(rule => rule.ParameterName, StringComparer.Ordinal)
            .ThenBy(rule => rule.ParameterType)
            .ThenBy(rule => rule.Condition.mode)
            .ThenBy(rule => rule.Condition.threshold)
            .Select(rule => rule.Condition);
    }
}
